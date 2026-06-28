#esta es la version ultima para raspberry pi pico

import time
import ubinascii
import machine
import micropython
#import esp
from machine import Pin
#esp.osdebug(None)
import gc
gc.collect()
from machine import Pin, I2C
from ssd1306 import SSD1306_I2C

oledProfe = SSD1306_I2C(128, 64, I2C(1))  

botonatras = machine.Pin(12, machine.Pin.IN) 
botonalante = machine.Pin(13, machine.Pin.IN)

shot_number = 0

tiempo_atras = 0.0
tiempo_alante = 0.0

metros_segundo = 0
acum_metros_segundo = 0

#calcular velocidad  y potencia (mps y julios)
  
def calculo():
    global tiempo_atras, tiempo_alante, metros_segundo, acum_metros_segundo, shot_number
    tiempo = tiempo_alante - tiempo_atras
    if tiempo != 0:    
        metros_segundo = 0.06/tiempo*1000000
        
    acum_metros_segundo = acum_metros_segundo + metros_segundo
    media_metros_segundo = acum_metros_segundo / shot_number


#enviar valores a OLED
   
    print ('disparo numero: ', shot_number,'metros / segundo',metros_segundo, 'media', media_metros_segundo)
    
    oledProfe.fill(0)
    oledProfe.text('DISPARO NUMERO: ', 0, 12)
    oledProfe.text(str(shot_number),50, 25)
    oledProfe.text("MPS : " + str(metros_segundo),0,40)
    oledProfe.text('MEDIA: ' + str(media_metros_segundo), 0, 50)
    oledProfe.show()
    

    
#leer sensores y calcular velocidad y potencia
    
while True:
      
    while  botonatras.value() == 1:
        uno = 1
        
    tiempo_atras = time.ticks_us()
    
    while  botonalante.value() == 1:
        uno = 1
        
    tiempo_alante = time.ticks_us()
    
    shot_number = shot_number + 1
    
    calculo()
    
    