package com.luis.cronografo

import android.bluetooth.BluetoothDevice
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.luis.cronografo.ble.BleManager
import com.luis.cronografo.tts.VoiceAnnouncer
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch
import java.util.Locale

class MainViewModel(
    val bleManager: BleManager,
    private val voiceAnnouncer: VoiceAnnouncer
) : ViewModel() {

    private val _isScanning = MutableStateFlow(false)
    val isScanning: StateFlow<Boolean> = _isScanning.asStateFlow()

    private val _currentLanguage = MutableStateFlow("ca")
    val currentLanguage: StateFlow<String> = _currentLanguage.asStateFlow()

    private val _statusText = MutableStateFlow<UiText>(UiText.StringResource(R.string.status_disconnected))
    val statusText: StateFlow<UiText> = _statusText.asStateFlow()

    val devices = bleManager.devices
    val isConnected = bleManager.isConnected

    private val _pelletWeightText = MutableStateFlow("0.52")
    val pelletWeightText: StateFlow<String> = _pelletWeightText.asStateFlow()

    private val _shots = MutableStateFlow<List<ShotModel>>(emptyList())
    val shots: StateFlow<List<ShotModel>> = _shots.asStateFlow()

    private val _lastShotNumber = MutableStateFlow<UiText>(UiText.DynamicString("-"))
    val lastShotNumber: StateFlow<UiText> = _lastShotNumber.asStateFlow()

    private val _lastShotVelocity = MutableStateFlow("0.0")
    val lastShotVelocity: StateFlow<String> = _lastShotVelocity.asStateFlow()

    private val _lastShotEnergy = MutableStateFlow("0.0")
    val lastShotEnergy: StateFlow<String> = _lastShotEnergy.asStateFlow()

    private val _avgVelocity = MutableStateFlow("0.0")
    val avgVelocity: StateFlow<String> = _avgVelocity.asStateFlow()
    private val _minVelocity = MutableStateFlow("0.0")
    val minVelocity: StateFlow<String> = _minVelocity.asStateFlow()
    private val _maxVelocity = MutableStateFlow("0.0")
    val maxVelocity: StateFlow<String> = _maxVelocity.asStateFlow()

    private val _avgEnergy = MutableStateFlow("0.0")
    val avgEnergy: StateFlow<String> = _avgEnergy.asStateFlow()
    private val _minEnergy = MutableStateFlow("0.0")
    val minEnergy: StateFlow<String> = _minEnergy.asStateFlow()
    private val _maxEnergy = MutableStateFlow("0.0")
    val maxEnergy: StateFlow<String> = _maxEnergy.asStateFlow()

    private var receivedBuffer = ""
    private var currentParsingNumber: Int? = null
    private var currentParsingVelocity: Float? = null
    private var currentParsingEnergy: Float? = null

    init {
        viewModelScope.launch {
            bleManager.incomingData.collectLatest { data ->
                receivedBuffer += data
                processBuffer()
            }
        }
    }

    fun startScan() {
        _isScanning.value = true
        _statusText.value = UiText.StringResource(R.string.status_scanning)
        bleManager.startScan()
        
        // Stop scan after 10s
        viewModelScope.launch {
            kotlinx.coroutines.delay(10000)
            if (_isScanning.value) {
                stopScan()
            }
        }
    }

    fun stopScan() {
        _isScanning.value = false
        bleManager.stopScan()
        if (!isConnected.value) {
            _statusText.value = UiText.StringResource(R.string.status_scan_finished)
        }
    }

    fun connect(device: BluetoothDevice) {
        receivedBuffer = ""
        currentParsingNumber = null
        currentParsingVelocity = null
        currentParsingEnergy = null

        val deviceName = device.name ?: "Dispositivo"
        _statusText.value = UiText.StringResource(R.string.status_connecting, deviceName)
        bleManager.connect(device)
        _statusText.value = UiText.StringResource(R.string.status_connected, deviceName)
        
        viewModelScope.launch {
            kotlinx.coroutines.delay(1000)
            bleManager.sendCommand("peso")
        }
    }

    fun disconnect() {
        bleManager.disconnect()
        _statusText.value = UiText.StringResource(R.string.status_disconnected)
    }

    fun setLanguage(langCode: String) {
        _currentLanguage.value = langCode
        voiceAnnouncer.setLanguage(langCode)
    }

    fun updateWeightText(weight: String) {
        _pelletWeightText.value = weight
    }

    fun sendWeight() {
        val weight = _pelletWeightText.value.toFloatOrNull()
        if (weight != null && weight > 0) {
            bleManager.sendCommand("peso ${String.format(Locale.US, "%.2f", weight)}")
            _statusText.value = UiText.StringResource(R.string.status_weight_updated, weight.toString())
        }
    }

    fun resetSession() {
        receivedBuffer = ""
        currentParsingNumber = null
        currentParsingVelocity = null
        currentParsingEnergy = null
        
        bleManager.sendCommand("reset")
        _shots.value = emptyList()
        recalculateStatistics()
        _lastShotNumber.value = UiText.DynamicString("-")
        _lastShotVelocity.value = "0.0"
        _lastShotEnergy.value = "0.0"
        _statusText.value = UiText.StringResource(R.string.status_session_reset)
    }

    private fun processBuffer() {
        // Reemplazar \r con \n para manejar saltos de línea irregulares del hardware
        receivedBuffer = receivedBuffer.replace('\r', '\n')
        
        while (true) {
            val newlineIdx = receivedBuffer.indexOf('\n')
            if (newlineIdx == -1) break
            
            val line = receivedBuffer.substring(0, newlineIdx).trim()
            receivedBuffer = receivedBuffer.substring(newlineIdx + 1)
            
            if (line.isNotEmpty()) {
                // Si el hardware concatenó "Disparo #" con un texto anterior (ej. por faltar un \n)
                var cleanLine = line
                val disparoIdx = cleanLine.indexOf("Disparo #", ignoreCase = true)
                if (disparoIdx > 0) {
                    val beforeDisparo = cleanLine.substring(0, disparoIdx).trim()
                    if (beforeDisparo.isNotEmpty()) {
                        parseLine(beforeDisparo)
                    }
                    cleanLine = cleanLine.substring(disparoIdx).trim()
                }
                
                parseLine(cleanLine)
            }
        }
    }

    private fun parseLine(line: String) {
        if (line.startsWith("Nuevo peso:", ignoreCase = true) || 
            line.startsWith("Peso actual:", ignoreCase = true)) {
            val regex = Regex("""\d+(\.\d+)?""")
            val match = regex.find(line)
            if (match != null) {
                _pelletWeightText.value = match.value
            }
            return
        }

        if (line.startsWith("Disparo #", ignoreCase = true)) {
            val numStr = line.substring(9)
            currentParsingNumber = numStr.toIntOrNull()
        } else if (line.endsWith("m/s", ignoreCase = true)) {
            val valStr = line.replace("m/s", "", ignoreCase = true).trim()
            currentParsingVelocity = valStr.toFloatOrNull()
        } else if (line.endsWith("Jul", ignoreCase = true)) {
            val valStr = line.replace("Jul", "", ignoreCase = true).trim()
            currentParsingEnergy = valStr.toFloatOrNull()
        }

        if (currentParsingNumber != null && currentParsingVelocity != null && currentParsingEnergy != null) {
            val newShot = ShotModel(
                number = currentParsingNumber!!,
                velocity = currentParsingVelocity!!,
                energy = currentParsingEnergy!!
            )
            
            val currentList = _shots.value.toMutableList()
            currentList.add(0, newShot)
            _shots.value = currentList
            
            _lastShotNumber.value = UiText.StringResource(R.string.shot_number, newShot.number.toString())
            _lastShotVelocity.value = String.format(Locale.US, "%.1f", newShot.velocity)
            _lastShotEnergy.value = String.format(Locale.US, "%.1f", newShot.energy)
            
            recalculateStatistics()
            voiceAnnouncer.announceShot(newShot.velocity, newShot.energy)

            currentParsingNumber = null
            currentParsingVelocity = null
            currentParsingEnergy = null
        }
    }

    private fun recalculateStatistics() {
        val shotsList = _shots.value
        if (shotsList.isEmpty()) {
            _avgVelocity.value = "0.0"
            _minVelocity.value = "0.0"
            _maxVelocity.value = "0.0"
            _avgEnergy.value = "0.0"
            _minEnergy.value = "0.0"
            _maxEnergy.value = "0.0"
            return
        }

        val velocities = shotsList.map { it.velocity }
        val energies = shotsList.map { it.energy }

        _avgVelocity.value = String.format(Locale.US, "%.1f", velocities.average())
        _minVelocity.value = String.format(Locale.US, "%.1f", velocities.minOrNull() ?: 0f)
        _maxVelocity.value = String.format(Locale.US, "%.1f", velocities.maxOrNull() ?: 0f)

        _avgEnergy.value = String.format(Locale.US, "%.1f", energies.average())
        _minEnergy.value = String.format(Locale.US, "%.1f", energies.minOrNull() ?: 0f)
        _maxEnergy.value = String.format(Locale.US, "%.1f", energies.maxOrNull() ?: 0f)
    }

    override fun onCleared() {
        super.onCleared()
        voiceAnnouncer.shutdown()
        bleManager.disconnect()
    }
}
