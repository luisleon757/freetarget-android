from machine import Pin
import time

# Configuración de pines
receptor_ir = Pin(6, Pin.IN, Pin.PULL_UP)  # Entrada del OPL550 en GP6 con pull-up interno
led_pico = Pin(21, Pin.OUT)   # LED integrado en la Pico
emisor_ir = Pin(15, Pin.OUT)  # Control del LED emisor IR

# Enciende el LED IR (si lo tienes conectado a GP15)
emisor_ir.value(1)

# Tiempo mínimo para confirmar el cambio de estado (en milisegundos)
FILTRO_TIEMPO = 20  # 20 ms debería ser suficiente para eliminar ruido

# Variable global para el último tiempo de cambio de estado
ultimo_tiempo = time.ticks_ms()

# Función que maneja la interrupción con filtrado de ruido
def detectar_ir(pin):
    global ultimo_tiempo
#     tiempo_actual = time.ticks_ms()
# 
#     # Filtrar cambios rápidos (posible ruido)
#     if time.ticks_diff(tiempo_actual, ultimo_tiempo) > FILTRO_TIEMPO:
    estado_sensor = pin.value()
#         
    if estado_sensor == 0:
        print("💡 Luz IR detectada")
        led_pico.value(1)  # Enciende el LED integrado
    else:
        print("🌑 No hay luz IR")
        led_pico.value(0)  # Apaga el LED integrado
    
        # Actualizar el tiempo del último cambio confirmado
#        ultimo_tiempo = tiempo_actual

# Configurar la interrupción en el pin del sensor con filtro de ruido
receptor_ir.irq(trigger=Pin.IRQ_FALLING | Pin.IRQ_RISING, handler=detectar_ir)

print("Esperando detección de luz IR...")

# Mantener el programa corriendo sin ocupar la CPU innecesariamente
while True:
    time.sleep(1)
