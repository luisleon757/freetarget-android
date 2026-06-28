import time
import network
import socket
from machine import Pin

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

# Configuración de los botones
botonatras = Pin(12, Pin.IN)
botonalante = Pin(13, Pin.IN)

# Variables globales
shot_number = 0
tiempo_atras = 0.0
tiempo_alante = 0.0
metros_segundo = 0
acum_metros_segundo = 0
masa = 0.01  # Masa del proyectil en kg

# Función para calcular velocidad y potencia
def calculo():
    global tiempo_atras, tiempo_alante, metros_segundo, acum_metros_segundo, shot_number
    tiempo = tiempo_alante - tiempo_atras
    if tiempo != 0:
        metros_segundo = 0.06 / tiempo * 1000000  # Velocidad en m/s

    acum_metros_segundo += metros_segundo
    media_metros_segundo = acum_metros_segundo / shot_number

    # Calcular energía en julios
    energia_julios = 0.5 * masa * (metros_segundo ** 2)

    return shot_number, metros_segundo, media_metros_segundo, energia_julios

# Función para generar la página HTML con fuente más grande
def generate_html(shot_number, mps, media, energia):
    html = f"""
    <!DOCTYPE html>
    <html>
    <head>
        <title>Datos del Disparo</title>
        <meta http-equiv="refresh" content="2">  <!-- Recarga la página cada 2 segundos -->
        <style>
            body {{ font-family: Arial, sans-serif; text-align: center; background-color: #f4f4f4; }}
            h1 {{ font-size: 40px; color: #333; }}
            p {{ font-size: 30px; color: #555; }}
        </style>
    </head>
    <body>
        <h1>Ultimo Disparo</h1>
        <p><strong>Disparo numero:</strong> {shot_number}</p>
        <p><strong>Velocidad (MPS):</strong> {mps:.2f}</p>
        <p><strong>Media (MPS):</strong> {media:.2f}</p>
        <p><strong>Energia (J):</strong> {energia:.4f}</p>
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
    # Leer sensores y calcular velocidad y potencia
    while botonatras.value() == 1:
        pass  # Esperar a que se suelte el botón

    tiempo_atras = time.ticks_us()

    while botonalante.value() == 1:
        pass  # Esperar a que se suelte el botón

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
#    client_socket.close()
