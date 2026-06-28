/*-------------------------------------------------------
 * 
 * timer_ISR.ino
 * 
 * Timer interrupt file
 * 
 * ----------------------------------------------------*/
#include "gpio.h"
#include "json.h"

/*
 * Definitions
 */
#define FREQUENCY 1000ul                // 1000 Hz
#define N_TIMERS  12                     // Keep space for 8 timrs
#define PORT_STATE_IDLE 0                       // There are no sensor inputs
#define PORT_STATE_WAIT 1                       // Some sensor inputs are present, but not all
#define PORT_STATE_DONE 2                       // All of the inmputs are present

#define MAX_WAIT_TIME   10                      // Wait up to 10 ms for the input to arrive

/*
 * Local Variables
 */
static unsigned long* timersensor[N_TIMERS]; // Active timer list
static unsigned long isr_timer;         // Elapsed time counter
static unsigned int isr_state = PORT_STATE_IDLE;// Current aquisition state
static bool timer2_divider = false;


/*-----------------------------------------------------
 * 
 * function: init_timer
 * 
 * brief: Initalize the timer channels
 * 
 * return: None
 * 
 *-----------------------------------------------------
 *
 * Timer 0 used to by Arduino for the millis() functions
 * Timer 2 is used by freETarget to sample the sensor inputs
 * Timer 1,3,4,5 are used for sensor detection
 *-----------------------------------------------------*/
void init_timer(void)
{
  int i;
  
  if ( DLT(DLT_CRITICAL) )
  {
    Serial.print(T("init_timer()"));
  }
  
/*
 * Timer 2 (instead of 1)
 */
  TCCR2A = 0;                           // set entire TCCR0A register to 0
  TCCR2B = 0;                           // same for TCCR0B
  TCNT2  = 0;                           // initialize counter value to 0

  OCR2A = 250; //16000000 / 64 / FREQUENCY;    // 16MHz CPU Clock / 64 Prescale / 1KHz timer interrupt
  TCCR2A |= (1 << WGM01);               // Enable CTC Mode
  TCCR2B |= (1 << CS01) | (1 << CS00);  // Prescale 64
  TIMSK2 &= ~(B0000010);                // Make sure the interrupt is disabled

  for (i=0; i != N_TIMERS; i++ )        // Clear the timer callback
  {
    timersensor[i] = 0;
  }

  timer_new(&isr_timer, 0);             // Setup two local timers

  // Init TCCRxA
  TCCR1A = 0;
  TCCR3A = 0;
  TCCR4A = 0;
  TCCR5A = 0;

  // Init TIMSKx: 7, 6, 5 - ICU, 4, 3, 2, 1, 0 - OVF
  TIMSK1 = B00100000;  // Enable Timer OVF & CAPT Interrupts
  TIMSK3 = B00100000;
  TIMSK4 = B00100000;
  TIMSK5 = B00100000;

/*
 * All done, return
 */  
  return;
}

/*-----------------------------------------------------
 * 
 * function: enable_timer_interrupt()
 * function: disable_timer_interrupt()
 * 
 * brief:    Turn on the interrupt enable bits
 * 
 * return: None
 * 
 *-----------------------------------------------------
 *
 * Set the CTC interrupt bit on or off
 * 
 *-----------------------------------------------------*/

void enable_timer_interrupt(void)
{
  TIMSK2 |= (B0000010);                       // Enable timer compare interrupt
  
/*
 * All done, return
 */  
  return;

}



void disable_timer_interrupt(void)
{
  TIMSK2 &= ~(B0000010);                      // disable timer compare interrupt
  
/*
 * All done, return
 */  
  return;
}


 /*-----------------------------------------------------
 *
 * Triggers when shot arrives, save value and stop timer
 * 
 *-----------------------------------------------------*/
ISR(TIMER1_CAPT_vect) {
  //Disable Interrupts
  TIMSK1 = B00000000;
  // stopt timer
  TCCR1B = B00000000;
  // transfer time into capture register
  T[0] = ICR1;
}

ISR(TIMER3_CAPT_vect) {
  //Disable Interrupts
  TIMSK3 = B00000000;
  // stopt timer
  TCCR3B = B00000000;
  // transfer time into capture register
  T[1] = ICR3;
}

ISR(TIMER4_CAPT_vect) {
  //Disable Interrupts
  TIMSK4 = B00000000;
  // stopt timer
  TCCR4B = B00000000;
  // transfer time into capture register
  T[2] = ICR4;
}

ISR(TIMER5_CAPT_vect) {
  //Disable Interrupts
  TIMSK5 = B00000000;
  // stopt timer
  TCCR5B = B00000000;
  // transfer time into capture register
  T[3] = ICR5;
}

/*-----------------------------------------------------
 * 
 * function: ISR
 * 
 * brief:    Timer 1 Interrupt
 * 
 * return:   None
 * 
 *-----------------------------------------------------
 *
 * Timer 1 samples the inputs and when all of the 
 * sendor inputs are present, the counters are
 * read and made available to the software
 * 
 * There are three data aquisition states
 * 
 * IDLE - No inputs are present
 * WAIT - Inputs are present, but we have to wait
 *        for all of the inputs to be present or
 *        timed out
 * DONE - We have read the counters but need to
 *        wait for the ringing to stop
 *        
 * There are three motor control states
 * 
 * IDLE    - Do nothing
 * RUNNING - The motor is turned on for a duration 
 * CYCLE   - Count the number of stepper motor cycles
 * 
 *-----------------------------------------------------*/

ISR(TIMER2_COMPA_vect)
{
  unsigned int pin;                             // Value read from the port
  unsigned char ch;                             // Byte input
  unsigned int  i;                              // Iteration counter
  
  TCNT2  = 0;                                   // Reset the counter back to 0

// trigger only every 2nd time to get 1 kHZ
if (timer2_divider) {
  timer2_divider = false;
  return;
}
timer2_divider = true;
/*
 * Refresh the timers
 */
  for (i=0; i != N_TIMERS; i++)
  {
    if ( (timersensor[i] != 0)
        && ( *timersensor[i] != 0 ) )
    {
      (*timersensor[i])--;
    }
  }
  
/*
 * Decide what to do if based on what inputs are present
 */ 
  pin = is_running();                 // Read in the RUN bits
  
/*
 * Read the timer hardware based on the ISR state
 */
  switch (isr_state)
  {
    case PORT_STATE_IDLE:                       // Idle, Wait for something to show up
      if ( pin != B00001111 )                           // Something has triggered, some timer have stopped
      { 
        isr_timer = (int)(json_sensor_dia / 0.30 / 1000.0) + 1; // Start the wait timer
        isr_state = PORT_STATE_WAIT;            // Got something wait for all of the sensors tro trigger
      }
      break;
          
    case PORT_STATE_WAIT:                       // Something is present, wait for all of the inputs
      if ( (pin == 0)                           // We have all of the inputs
          || (isr_timer == 0) )                 // or ran out of time.  Read the timers and restart
      {
        aquire();                               // Read the counters
        clear_running();                        // Reset the RUN flip Flop
        isr_timer = json_min_ring_time;         // Reset the timer
        isr_state = PORT_STATE_DONE;            // and wait for the all clear
      }
      break;
      
    case PORT_STATE_DONE:                       // Waiting for the ringing to stop
      if ( isr_timer == 0 )                   // Make sure there is no rigning
      {
        arm_timers();                         // and arm for the next time
        isr_state = PORT_STATE_IDLE;          // and go back to idle
      }
      break;
  }

/*
 * Spool the AUX input
 */
  while ( AUX_SERIAL.available() )
  {
    ch = AUX_SERIAL.read();     
    aux_spool_put(ch);
  }


/*
 * Undo the mutex and return
 */
  return;
}


/*-----------------------------------------------------
 * 
 * function: timer_new()
 *           timer_delete()
 * 
 * brief:    Add or remove timers
 *  
 * return:   TRUE if the operation was a success
 * 
 *-----------------------------------------------------
 *
 * The timer interrupt has the ability to manage a 
 * count down timer
 * 
 * These functions add or remove a timer from the active
 * timer list
 * 
 * IMPORTANT
 * 
 * The timers should be static variables, otherwise they
 * will overflow the available space every time they are
 * instantiated.
 *-----------------------------------------------------*/
unsigned int timer_new
(
  unsigned long* new_timer,         // Pointer to new down counter
  unsigned long  start_time         // Starting value
)
{
  unsigned int i;

  for (i=0;  i != N_TIMERS; i++ )   // Look through the space
  {
    if ( (timersensor[i] == 0)           // Got an empty timer slot
      || (timersensor[i] == new_timer) ) // or it already exists
    {
      timersensor[i] = new_timer;        // Add it in
      *timersensor[i] = start_time;
      return 1;
    }
  }

  if ( DLT(DLT_CRITICAL) )
  {
    Serial.print(T("No space for timer"));
  }
  
  return 0;
}

 unsigned int timer_delete
(
  unsigned long* old_timer          // Pointer to new down counter
)
{
  unsigned int i;

  for (i=0;  i != N_TIMERS; i++ )   // Look through the space
  {
    if ( timersensor[i] == old_timer )   // Found the existing timer
    {
      timersensor[i] = 0;                // Add it in
      return 1;
    }
  }

  return 0;
  
}
