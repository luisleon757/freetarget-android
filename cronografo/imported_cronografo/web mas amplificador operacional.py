import time
import network
import socket
from machine import Pin, ADC

# Configuración del Access Point (AP)
ssid = "PicoW_AP"  # Nombre de la red WiFi creada por la Pico W
password = "12345678"  # Contraseña de la red WiFi

ap = network.WLAN(network.AP_IF)
ap.active(True)
ap.config(essid=ssid, password=password)
while not ap.active():
    pass

print("Access Point activo")
print("IP:", ap.ifconfig()[0])

# Configuración de los sensores IR
sensor_atras = ADC(Pin(26))  # Sensor 1 conectado al pin ADC0 (GP26)
sensor_alante = ADC(Pin(27))  # Sensor 2 conectado al pin ADC1 (GP27)

# Umbrales de detección (ajustar según pruebas)
# umbral_atras = 30000
# umbral_alante = 30000
umbral_atras = 1000
umbral_alante = 1000
# Variables globales
shot_number = 0
tiempo_atras = 0.0
tiempo_alante = 0.0
metros_segundo = 0
acum_metros_segundo = 0
masa = 0.01  # Masa del proyectil en kg

def calculo():
    global tiempo_atras, tiempo_alante, metros_segundo, acum_metros_segundo, shot_number
    tiempo = tiempo_alante - tiempo_atras
    if tiempo > 0:
        metros_segundo = 0.06 / (tiempo / 1_000_000)  # Velocidad en m/s

    acum_metros_segundo += metros_segundo
    media_metros_segundo = acum_metros_segundo / shot_number
    
    # Calcular energía en julios
    energia_julios = 0.5 * masa * (metros_segundo ** 2)
    
    return shot_number, metros_segundo, media_metros_segundo, energia_julios

def generate_html(shot_number, mps, media, energia):
    html = f"""
    <!DOCTYPE html>
    <html>
    <head>
        <title>Datos del Disparo</title>
        <meta http-equiv="refresh" content="2">
        <style>
            body {{ font-family: Arial, sans-serif; text-align: center; background-color: #f4f4f4; }}
            h1 {{ font-size: 40px; color: #333; }}
            p {{ font-size: 30px; color: #555; }}
        </style>
    </head>
    <body>
        <h1>Último Disparo</h1>
        <p><strong>Disparo número:</strong> {shot_number}</p>
        <p><strong>Velocidad (MPS):</strong> {mps:.2f}</p>
        <p><strong>Media (MPS):</strong> {media:.2f}</p>
        <p><strong>Energía (J):</strong> {energia:.4f}</p>
    </body>
    </html>
    """
    return html

# Configurar el servidor web
server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server_socket.bind(('0.0.0.0', 80))
server_socket.listen(5)
print('Servidor web iniciado. Conéctate a la red WiFi y accede a la IP:', ap.ifconfig()[0])

# Bucle principal
while True:
    # Esperar a que el sensor trasero detecte el proyectil
    while sensor_atras.read_u16() > umbral_atras:
        pass
    tiempo_atras = time.ticks_us()

    # Esperar a que el sensor delantero detecte el proyectil
    while sensor_alante.read_u16() > umbral_alante:
        pass
    tiempo_alante = time.ticks_us()

    shot_number += 1

    # Obtener los datos actuales
    shot_number, mps, media, energia = calculo()

    # Esperar una conexión
    client_socket, addr = server_socket.accept()
    print('Conexión desde:', addr)

    # Generar la página HTML
    html = generate_html(shot_number, mps, media, energia)

    # Enviar la respuesta HTTP
    client_socket.send('HTTP/1.1 200 OK\n')
    client_socket.send('Content-Type: text/html\n')
    client_socket.send('Connection: close\n\n')
    client_socket.sendall(html)
    client_socket.close()

