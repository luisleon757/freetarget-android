#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>
#include <EEPROM.h>
#include <stdarg.h>
#include <math.h>

#define HIST_SIZE 10
float historial[HIST_SIZE] = {0};
int idxHistorial = 0;

// Función para redondear a 1 decimal
float redondear1Decimal(float valor) {
    return roundf(valor * 10.0) / 10.0;
}

bool esVelocidadRepetida(float nuevaVelocidad) {
    float nuevaRedondeada = redondear1Decimal(nuevaVelocidad);
    
    for (int i = 0; i < HIST_SIZE; i++) {
        float histRedondeado = redondear1Decimal(historial[i]);
        if (nuevaRedondeada == histRedondeado) {
            return true;
        }
    }
    return false;
}

void guardarVelocidad(float v) {
    historial[idxHistorial] = v;
    idxHistorial = (idxHistorial + 1) % HIST_SIZE;
}

// Variables BLE
BLEServer *pServer = NULL;
BLECharacteristic *pTxCharacteristic = NULL;
bool deviceConnected = false;
bool oldDeviceConnected = false;

// UUIDs estándar para Nordic UART Service (NUS)
#define SERVICE_UUID           "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"
#define RX_CHARACTERISTIC_UUID "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"
#define TX_CHARACTERISTIC_UUID "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"

String cmdBuffer = "";
bool cmdReady = false;

class MyServerCallbacks: public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) {
      deviceConnected = true;
    };

    void onDisconnect(BLEServer* pServer) {
      deviceConnected = false;
    }
};

class MyCharacteristicCallbacks: public BLECharacteristicCallbacks {
    void onWrite(BLECharacteristic *pCharacteristic) {
      String rxValue = pCharacteristic->getValue();

      if (rxValue.length() > 0) {
        for (int i = 0; i < rxValue.length(); i++) {
          char c = rxValue[i];
          if (c == '\n') {
            cmdReady = true;
          } else {
            cmdBuffer += c;
          }
        }
      }
    }
};

const int sensorA = 33;
const int sensorB = 32;

#define EEPROM_SIZE 64
#define PESO_ADDR 0
#define CAL_ADDR 4

float distanciaSensores = 0.06;
float pesoGramos = 0.52;
float factorCalibracion = 1.0;

float energiaTotal = 0;
float energiaMin = 1000000;
float energiaMax = 0;

float velocidadTotal = 0;
float velocidadMin = 1000000;
float velocidadMax = 0;

int numDisparos = 0;

bool esperandoSensorA = true;
bool esperandoSensorB = false;

unsigned long tiempoA = 0;
unsigned long tiempoB = 0;

bool prevA = LOW;
bool prevB = LOW;

const unsigned long TIMEOUT_MS = 1000;
unsigned long tiempoInicioEspera = 0;

void btPrint(const String& mensaje) {
    if (deviceConnected && pTxCharacteristic != NULL) {
        pTxCharacteristic->setValue(mensaje);
        pTxCharacteristic->notify();
        delay(10); // Pausa para evitar desbordar el búfer BLE
    }
}

void btPrintln(const String& mensaje) {
    if (deviceConnected && pTxCharacteristic != NULL) {
        String msg = mensaje + "\n";
        pTxCharacteristic->setValue(msg);
        pTxCharacteristic->notify();
        delay(10);
    }
}

void btPrintf(const char* format, ...) {
    if (deviceConnected && pTxCharacteristic != NULL) {
        char buffer[256];
        va_list args;
        va_start(args, format);
        vsnprintf(buffer, sizeof(buffer), format, args);
        va_end(args);
        
        pTxCharacteristic->setValue(String(buffer));
        pTxCharacteristic->notify();
        delay(10);
    }
}

void guardarPesoEEPROM(float peso) {
    EEPROM.put(PESO_ADDR, peso);
    EEPROM.commit();
}

void guardarCalibracionEEPROM(float cal) {
    EEPROM.put(CAL_ADDR, cal);
    EEPROM.commit();
}

float leerPesoEEPROM() {
    float peso = 0.52;
    EEPROM.get(PESO_ADDR, peso);
    return (peso <= 0 || peso > 1000) ? 0.52 : peso;
}

float leerCalibracionEEPROM() {
    float cal = 1.0;
    EEPROM.get(CAL_ADDR, cal);
    return (cal < 0.5 || cal > 1.5) ? 1.0 : cal;
}

void setup() {
    EEPROM.begin(EEPROM_SIZE);
    pinMode(sensorA, INPUT);
    pinMode(sensorB, INPUT);

    // Inicializar historial con valores únicos
    for (int i = 0; i < HIST_SIZE; i++) {
        historial[i] = 1000.0 + i;
    }
    
    Serial.begin(115200);

    // Inicializar BLE
    BLEDevice::init("CronoABC");
    pServer = BLEDevice::createServer();
    pServer->setCallbacks(new MyServerCallbacks());

    // Crear el Servicio UART
    BLEService *pService = pServer->createService(SERVICE_UUID);

    // Crear la Característica TX (para enviar notificaciones)
    pTxCharacteristic = pService->createCharacteristic(
                        TX_CHARACTERISTIC_UUID,
                        BLECharacteristic::PROPERTY_NOTIFY
                      );
    pTxCharacteristic->addDescriptor(new BLE2902());

    // Crear la Característica RX (para recibir escrituras)
    BLECharacteristic *pRxCharacteristic = pService->createCharacteristic(
                         RX_CHARACTERISTIC_UUID,
                         BLECharacteristic::PROPERTY_WRITE
                       );
    pRxCharacteristic->setCallbacks(new MyCharacteristicCallbacks());

    // Iniciar el servicio y publicidad BLE
    pService->start();
    pServer->getAdvertising()->addServiceUUID(SERVICE_UUID);
    pServer->getAdvertising()->start();
    
    pesoGramos = leerPesoEEPROM();
    factorCalibracion = leerCalibracionEEPROM();
    
    Serial.printf("BLE Iniciado como 'CronoABC'. Peso inicial: %.2f g, Calibración: %.1f%%\n", 
                  pesoGramos, (factorCalibracion - 1.0) * 100.0);
}

void loop() {
    // Reconexión publicitaria automática si se desconecta
    if (!deviceConnected && oldDeviceConnected) {
        delay(500);
        pServer->startAdvertising();
        Serial.println("Comenzando anuncio BLE de nuevo...");
        oldDeviceConnected = deviceConnected;
    }
    if (deviceConnected && !oldDeviceConnected) {
        oldDeviceConnected = deviceConnected;
        Serial.println("Cliente BLE conectado!");
    }

    bool estadoA = digitalRead(sensorA);
    bool estadoB = digitalRead(sensorB);

    if (esperandoSensorA) {
        if (estadoA == HIGH && prevA == LOW) {
            tiempoA = micros();
            esperandoSensorA = false;
            esperandoSensorB = true;
            tiempoInicioEspera = millis();
        }
    }
    else if (esperandoSensorB) {
        if (estadoB == HIGH && prevB == LOW) {
            tiempoB = micros();
            unsigned long dt = tiempoB - tiempoA;

            if (dt >= 50 && dt <= 1000000) {
                float tiempoSegundos = dt / 1000000.0;
                float velocidadBase = distanciaSensores / tiempoSegundos;
                float velocidad = velocidadBase * factorCalibracion;
                float masa = pesoGramos / 1000.0;
                float energia = 0.5 * masa * velocidad * velocidad;

                // Actualizar estadísticas con valor original
                energiaTotal += energia;
                velocidadTotal += velocidad;
                numDisparos++;

                if (velocidad < velocidadMin) velocidadMin = velocidad;
                if (velocidad > velocidadMax) velocidadMax = velocidad;
                if (energia < energiaMin) energiaMin = energia;
                if (energia > energiaMax) energiaMax = energia;
                
                // Ajuste de velocidad para evitar duplicados
                float velocidadPublicada = velocidad;
                float energiaPublicada = energia;
                
                // Asegurar velocidad única (comparando con 1 decimal)
                while (esVelocidadRepetida(velocidadPublicada)) {
                    velocidadPublicada += 0.3;
                }
                
                // Guardar en historial la velocidad publicada
                guardarVelocidad(velocidadPublicada);
                
                // Recalcular energía con velocidad final
                energiaPublicada = 0.5 * masa * velocidadPublicada * velocidadPublicada;

                // Publicar valores con 1 decimal
                btPrintf("Disparo #%d\n", numDisparos);
                btPrintf(" %.1f m/s\n", velocidadPublicada);
                btPrintf(" %.1f Jul\n\n", energiaPublicada);

                Serial.printf("Disparo #%d | V: %.1f m/s | E: %.1f J\n", numDisparos, velocidadPublicada, energiaPublicada);
            } else {
                btPrintf("Error: dt fuera de rango (%lu us)\n", dt);
                Serial.printf("Error: dt fuera de rango (%lu us)\n", dt);
            }

            esperandoSensorA = true;
            esperandoSensorB = false;
        }
        else if (millis() - tiempoInicioEspera > TIMEOUT_MS) {
            btPrintln("Timeout: El segundo sensor no fue detectado.");
            Serial.println("Timeout: El segundo sensor no fue detectado.");
            esperandoSensorA = true;
            esperandoSensorB = false;
        }
    }

    prevA = estadoA;
    prevB = estadoB;

    // Procesar comandos entrantes recibidos por BLE RX
    if (cmdReady) {
        String comando = cmdBuffer;
        comando.trim();
        cmdBuffer = "";
        cmdReady = false;

        Serial.printf("Comando BLE recibido: %s\n", comando.c_str());

        if (comando.startsWith("peso")) {
            if (comando.length() > 5) {
                float nuevoPeso = comando.substring(5).toFloat();
                if (nuevoPeso > 0 && nuevoPeso <= 1000) {
                    pesoGramos = nuevoPeso;
                    guardarPesoEEPROM(pesoGramos);
                    btPrintf("Nuevo peso: %.2f gramos\n", pesoGramos);
                } else {
                    btPrintln("Error: Peso debe ser 0.01-1000g");
                }
            } else {
                btPrintf("Peso actual: %.2f gramos\n", pesoGramos);
            }
        } 
        else if (comando == "reset") {
            energiaTotal = 0;
            velocidadTotal = 0;
            numDisparos = 0;
            energiaMin = 1000000;
            energiaMax = 0;
            velocidadMin = 1000000;
            velocidadMax = 0;
            btPrintln("Estadísticas reiniciadas");
        } 
        else if (comando == "stats") {
            if (numDisparos > 0) {
                float velProm = velocidadTotal / numDisparos;
                float eneProm = energiaTotal / numDisparos;
                
                btPrintf("Disparos: %d\n", numDisparos);
                btPrintf("ms: Prom %.1f    | Min %.1f | Max %.1f\n",
                        velProm, velocidadMin, velocidadMax);
                btPrintf("Jul: Prom %.1f      | Min %.1f | Max %.1f\n",
                        eneProm, energiaMin, energiaMax);
            } else {
                btPrintln("No hay datos estadísticos");
            }
        }
        else if (comando.startsWith("CAL")) {
            if (comando.length() > 4) {
                String valorStr = comando.substring(4);
                valorStr.trim();
                
                if (valorStr.length() > 0) {
                    float ajuste = valorStr.toFloat() / 100.0;
                    factorCalibracion = 1.0 + ajuste;
                    
                    if (factorCalibracion < 0.5) factorCalibracion = 0.5;
                    if (factorCalibracion > 1.5) factorCalibracion = 1.5;
                    
                    guardarCalibracionEEPROM(factorCalibracion);
                    btPrintf("Calibración ajustada: %.1f%%\n", ajuste * 100.0);
                    btPrintf("Factor actual: %.1f\n", factorCalibracion);
                } else {
                    btPrintf("Calibración actual: %.1f%%\n", 
                             (factorCalibracion - 1.0) * 100.0);
                }
            } else {
                btPrintf("Calibración actual: %.1f%%\n", 
                         (factorCalibracion - 1.0) * 100.0);
            }
        }
        else if (comando == "help" || comando == "?") {
            btPrintln("Comandos disponibles:");
            btPrintln("peso [valor] - Mostrar/cambiar peso (ej: peso 0.45)");
            btPrintln("CAL [+-valor] - Ajuste calibración velocidad (ej: CAL +2.5)");
            btPrintln("reset - Reiniciar estadísticas");
            btPrintln("stats - Mostrar estadísticas");
        }
        else {
            btPrintln("Comando desconocido. Envie 'help' para ayuda");
        }
    }
}