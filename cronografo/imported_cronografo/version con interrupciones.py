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

# Configuración de los sensores OP550
sensor_atras = Pin(12, Pin.IN)
sensor_alante = Pin(13, Pin.IN)

# Variables globales
shot_number = 1
tiempo_atras = 0
tiempo_alante = 0
metros_segundo = 0
acum_metros_segundo = 0
masa = 0.01  # Masa del proyectil en kg

def sensor_atras_handler(pin):
    global tiempo_atras
    tiempo_atras = time.ticks_us()

def sensor_alante_handler(pin):
    global tiempo_alante, shot_number
    tiempo_alante = time.ticks_us()
    shot_number += 1

sensor_atras.irq(trigger=Pin.IRQ_FALLING, handler=sensor_atras_handler)
sensor_alante.irq(trigger=Pin.IRQ_FALLING, handler=sensor_alante_handler)

# Función para calcular velocidad y potencia
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

# Función para generar la página HTML
def generate_html():
    html = """<!DOCTYPE html>
    <html>
    <head>
        <title>Datos del Disparo</title>
        <style>
            body { font-family: Arial, sans-serif; text-align: center; background-color: #f4f4f4; }
            h1 { font-size: 80px; color: #333; }
            p { font-size: 60px; color: #555; }
        </style>
        <script>
            function actualizarDatos() {
                fetch('/datos').then(response => response.json()).then(data => {
                    document.getElementById('disparo').innerText = data.shot_number;
                    document.getElementById('velocidad').innerText = data.mps.toFixed(2);
                    document.getElementById('media').innerText = data.media.toFixed(2);
                    document.getElementById('energia').innerText = data.energia.toFixed(4);
                });
            }
            setInterval(actualizarDatos, 2000);  // Reducir frecuencia de actualización a cada 2 segundos
        </script>
    </head>
    <body>
        <h1>Último Disparo</h1>
        <p><strong>Disparo número:</strong> <span id="disparo">0</span></p>
        <p><strong>Velocidad (MPS):</strong> <span id="velocidad">0.00</span></p>
        <p><strong>Media (MPS):</strong> <span id="media">0.00</span></p>
        <p><strong>Energía (J):</strong> <span id="energia">0.0000</span></p>
    </body>
    </html>"""
    return html

# Configurar el servidor web
server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
server_socket.bind(('0.0.0.0', 80))
#server_socket.listen(2)  # Limitar el número de conexiones en cola
print('Servidor web iniciado. Conéctate a la red WiFi y accede a la IP:', ap.ifconfig()[0])

# Bucle principal
while True:
    try:
        client_socket, addr = server_socket.accept()
        print('Conexión desde:', addr)
        
        while True:
            request = client_socket.recv(1024).decode()
            
            if "GET /datos" in request:
                shot_number, mps, media, energia = calculo()
                json_response = f'{{"shot_number": {shot_number}, "mps": {mps}, "media": {media}, "energia": {energia}}}'
                
                client_socket.send('HTTP/1.1 200 OK\n')
                client_socket.send('Content-Type: application/json\n')
                client_socket.send('Connection: keep-alive\n\n')
                client_socket.sendall(json_response)
            else:
                html = generate_html()
                
                client_socket.send('HTTP/1.1 200 OK\n')
                client_socket.send('Content-Type: text/html\n')
                client_socket.send('Connection: keep-alive\n\n')
                client_socket.sendall(html)
    except Exception as e:
        print("Error en la conexión:", e)
