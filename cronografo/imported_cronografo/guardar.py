import machine, utime, network, socket

# Variables globales
time_sensor1 = None
time_sensor2 = None
distance = 0.06  # metros
masa = 0.01  # kg
disparo = 0  # contador disparos
ultimo_disparo = False  # Control de actualización

# Configuración de sensores
sensor_atras = machine.Pin(6, machine.Pin.IN, machine.Pin.PULL_DOWN)
sensor_delante = machine.Pin(7, machine.Pin.IN, machine.Pin.PULL_DOWN)

# Manejadores de interrupción
def callback_sensor_atras(pin):
    global time_sensor1
    if time_sensor1 is None:
        time_sensor1 = utime.ticks_us()

def callback_sensor_delante(pin):
    global time_sensor1, time_sensor2, ultimo_disparo
    if time_sensor1 is not None and time_sensor2 is None:
        time_sensor2 = utime.ticks_us()
        ultimo_disparo = True  # Indicar que hay un nuevo disparo

sensor_atras.irq(trigger=machine.Pin.IRQ_RISING, handler=callback_sensor_atras)
sensor_delante.irq(trigger=machine.Pin.IRQ_RISING, handler=callback_sensor_delante)

def calcular_resultados():
    global time_sensor1, time_sensor2
    if time_sensor1 and time_sensor2:
        dt = utime.ticks_diff(time_sensor2, time_sensor1) / 1_000_000
        if dt > 0:
            velocidad = distance / dt
            energia = 0.5 * masa * velocidad ** 2
            time_sensor1, time_sensor2 = None, None
            return velocidad, energia
    return None, None

# Configuración del punto de acceso
ap = network.WLAN(network.AP_IF)
ap.config(essid='Cronografo_AP', password='12345678')
ap.active(True)

def serve_client(client):
    global masa, disparo, ultimo_disparo
    try:
        request = client.recv(1024).decode('utf-8')
        
        # Enviar datos solo si hubo un disparo
        if 'GET /data' in request and ultimo_disparo:
            velocidad, energia = calcular_resultados()
            if velocidad is not None and energia is not None:
                disparo += 1
                response = f'{{"disparo": {disparo},"velocidad": {velocidad:.2f}, "energia": {energia:.2f}, "masa": {masa:.2f}}}'
                ultimo_disparo = False  # Resetear el estado del disparo
            else:
                response = '{"message": "Esperando disparo..."}'
            
            client.send("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n\r\n")
            client.send(response.encode('utf-8'))
            client.close()
            return
        
        # Manejo de parámetros de masa
        if 'GET /?masa=' in request:
            try:
                masa_str = request.split('masa=')[1].split('&')[0].split(' ')[0]
                masa = float(masa_str)
            except (IndexError, ValueError) as e:
                print("Error al procesar masa:", e)

        # Generar página HTML
        html = f"""<!DOCTYPE html>
<html>
<head>
    <title>Cronógrafo</title>
    <meta charset="UTF-8">
    <script>
        function verificarDisparo() {{
            fetch('/data')
                .then(r => r.json())
                .then(data => {{
                    if (data.velocidad !== undefined) {{
                        document.getElementById('resultado').innerHTML = `
                            <p>Disparo: ${{data.disparo}}</p>
                            <p>Velocidad: ${{data.velocidad.toFixed(2)}} m/s</p>
                            <p>Energía: ${{data.energia.toFixed(2)}} J</p>
                            <p>Masa actual: ${{data.masa.toFixed(2)}} kg</p>`;
                    }}
                }})
                .catch(e => console.log('Error:', e));
        }}
        setInterval(verificarDisparo, 500);
    </script>
</head>
<body>
    <h1>Cronógrafo de Proyectiles</h1>
    <form action="/" method="get">
        <label>Masa (kg):</label>
        <input type="number" step="0.01" name="masa" value="{masa:.2f}">
        <button type="submit">Actualizar Masa</button>
    </form>
    <div id="resultado"><p>Aguardando disparo...</p></div>
</body>
</html>"""
        
        client.send("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\n\r\n")
        client.send(html.encode('utf-8'))
        
    except Exception as e:
        print("Error manejando cliente:", str(e))
    finally:
        client.close()

# Iniciar servidor
addr = socket.getaddrinfo('0.0.0.0', 80)[0][-1]
s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
s.bind(addr)
s.listen(5)
print('Servidor iniciado:', ap.ifconfig())

# Bucle principal
while True:
    try:
        client, addr = s.accept()
        serve_client(client)
    except Exception as e:
        print("Error en bucle principal:", str(e))
