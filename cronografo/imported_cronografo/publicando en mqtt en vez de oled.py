import time
import machine
import network
from umqtt import MQTTClient

# Configuración del broker MQTT
MQTT_BROKER = "broker.emqx.io"
MQTT_PORT = 1883
MQTT_TOPIC = "diana/electronica"

# Configuración de WiFi
# WIFI_SSID = "Xiaomi_C10C"
# WIFI_PASSWORD = "ZXUmPEFE"
WIFI_SSID = "HUAWEI P30"
WIFI_PASSWORD = "ZXUmPEFE"
# Conectar a WiFi
def conectar_wifi():
    wlan = network.WLAN(network.STA_IF)
    wlan.active(True)
    wlan.connect(WIFI_SSID, WIFI_PASSWORD)
    while not wlan.isconnected():
        pass
    print("Conectado a WiFi:", wlan.ifconfig())

conectar_wifi()

# Configurar cliente MQTT
client = MQTTClient("", MQTT_BROKER, MQTT_PORT)

client.connect()

botonatras = machine.Pin(12, machine.Pin.IN) 
botonalante = machine.Pin(13, machine.Pin.IN)

shot_number = 0
tiempo_atras = 0.0
tiempo_alante = 0.0
metros_segundo = 0
acum_metros_segundo = 0

def calculo():
    global tiempo_atras, tiempo_alante, metros_segundo, acum_metros_segundo, shot_number, client

    tiempo = tiempo_alante - tiempo_atras
    if tiempo != 0:    
        metros_segundo = 0.06 / tiempo * 1000000
    
    energia_julios = 0.5 * 0.0006 * (metros_segundo ** 2)  # Cálculo de energía en julios
    
    acum_metros_segundo += metros_segundo
    media_metros_segundo = acum_metros_segundo / shot_number

    # Verificar si el cliente está conectado, si no, reconectar
    try:
        client.ping()  # Comprobar si sigue conectado
    except:
        print("Reconectando a MQTT...")
        client.connect()

    # Publicar datos en MQTT

    mensaje = f"tiro: {shot_number}, mps: {metros_segundo:.2f}, Media: {media_metros_segundo:.2f}, Julios: {energia_julios:.4f}"
    time.sleep(1)
    try:
        client.publish(MQTT_TOPIC, mensaje)
        print("Publicado:", mensaje, MQTT_TOPIC)
    except Exception as e:
        print("Error publicando MQTT:", e)


# Leer sensores y calcular velocidad y potencia
while True:
    while botonatras.value() == 1:
        pass
        
    tiempo_atras = time.ticks_us()
    
    while botonalante.value() == 1:
        pass
        
    tiempo_alante = time.ticks_us()
    
    shot_number += 1
    calculo()
