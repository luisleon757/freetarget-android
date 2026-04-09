using System;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace freETargetMAUI.Services;

public class ShotReceivedEventArgs : EventArgs
{
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public int ShotNumber { get; set; }
}

public class TargetConnectionService
{
    private TcpClient _client;
    private NetworkStream _stream;
    private StreamReader _reader;
    private CancellationTokenSource _cts;

    public event EventHandler<ShotReceivedEventArgs> OnShotReceived;
    public event EventHandler<string> OnConnectionStateChanged;
    public event EventHandler<string> OnRawDataReceived;
    
    public string IPAddress { get; set; } = "192.168.10.9"; 
    public int Port { get; set; } = 1090;

    public bool IsConnected => _client?.Connected ?? false;

    public async Task ConnectAsync()
    {
        if (IsConnected) return;

        try
        {
            OnConnectionStateChanged?.Invoke(this, "Conectando...");
            _client = new TcpClient();
            
            var connectTask = _client.ConnectAsync(IPAddress, Port);
            var timeoutTask = Task.Delay(8000); 
            if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
            {
                throw new TimeoutException("Timeout al conectar con el blanco.");
            }
            if (_client.Connected)
            {
               _stream = _client.GetStream();
               _reader = new StreamReader(_stream);
               _cts = new CancellationTokenSource();
               
               OnConnectionStateChanged?.Invoke(this, "Conectado");
               
               // Enviar configuración al hardware (fijar el Sensor Diagonal a 230mm)
               await SendMessageAsync("{\"SENSOR_DIA\": 230.0}");
               
               // Inicia el bucle de lectura en background
               _ = ReadLoopAsync(_cts.Token);
            }
        }
        catch (Exception ex)
        {
            Disconnect();
            OnConnectionStateChanged?.Invoke(this, $"Error: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        _reader?.Dispose();
        _stream?.Dispose();
        _client?.Close();
        _client = null;
        
        OnConnectionStateChanged?.Invoke(this, "Desconectado");
    }

    public async Task SendMessageAsync(string message)
    {
        if (IsConnected && _stream != null)
        {
            try
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes(message + "\r\n");
                await _stream.WriteAsync(data, 0, data.Length);
                await _stream.FlushAsync();
                System.Diagnostics.Debug.WriteLine($"Enviado: {message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enviando mensaje: {ex.Message}");
            }
        }
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        var buffer = new System.Text.StringBuilder();
        byte[] readBuffer = new byte[2048];
        
        try
        {
            while (!token.IsCancellationRequested && IsConnected)
            {
                int bytesRead = await _stream.ReadAsync(readBuffer, 0, readBuffer.Length, token);
                if (bytesRead <= 0) break;

                string chunk = System.Text.Encoding.UTF8.GetString(readBuffer, 0, bytesRead);
                buffer.Append(chunk);
                
                string currentContent = buffer.ToString();
                OnRawDataReceived?.Invoke(this, $"[Chunk recibida]: {chunk}");

                // Buscamos un bloque JSON completo { ... }
                // El hardware suele mandar \r\n{ ... }\r\n
                var match = System.Text.RegularExpressions.Regex.Match(currentContent, @"\{[^{}]+\}");
                
                if (match.Success)
                {
                    string jsonBlock = match.Value;
                    
                    try
                    {
                        var xMatch = System.Text.RegularExpressions.Regex.Match(jsonBlock, "\"x\"\\s*:\\s*([-0-9.]+)");
                        var yMatch = System.Text.RegularExpressions.Regex.Match(jsonBlock, "\"y\"\\s*:\\s*([-0-9.]+)");
                        var shotMatch = System.Text.RegularExpressions.Regex.Match(jsonBlock, "\"shot\"\\s*:\\s*(\\d+)");

                        if (xMatch.Success && yMatch.Success)
                        {
                            if (decimal.TryParse(xMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal xVal) &&
                                decimal.TryParse(yMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal yVal))
                            {
                                int shotNumber = 0;
                                if (shotMatch.Success)
                                {
                                    int.TryParse(shotMatch.Groups[1].Value, out shotNumber);
                                }

                                var args = new ShotReceivedEventArgs
                                {
                                    X = xVal,
                                    Y = yVal,
                                    ShotNumber = shotNumber
                                };
                                OnShotReceived?.Invoke(this, args);
                                OnRawDataReceived?.Invoke(this, $"[PARSED] Shot {shotNumber}: X={xVal}, Y={yVal}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error parseando JSON block: {ex.Message}");
                    }

                    // Limpiamos lo procesado del buffer
                    buffer.Remove(0, match.Index + match.Length);
                }

                // Evitar que el buffer crezca infinitamente si hay basura
                if (buffer.Length > 8192) buffer.Clear();
            }
        }
        catch (OperationCanceledException) {  }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                OnConnectionStateChanged?.Invoke(this, $"Error lectura: {ex.Message}");
                Disconnect();
            }
        }
    }
}
