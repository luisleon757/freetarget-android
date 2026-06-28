package com.luis.cronografo

import android.Manifest
import android.annotation.SuppressLint
import android.content.res.Configuration
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import com.google.accompanist.permissions.ExperimentalPermissionsApi
import com.google.accompanist.permissions.rememberMultiplePermissionsState
import com.luis.cronografo.ble.BleManager
import com.luis.cronografo.tts.VoiceAnnouncer
import java.util.Locale

class MainActivity : ComponentActivity() {
    private lateinit var bleManager: BleManager
    private lateinit var voiceAnnouncer: VoiceAnnouncer
    private lateinit var viewModel: MainViewModel

    @OptIn(ExperimentalPermissionsApi::class)
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        bleManager = BleManager(this)
        voiceAnnouncer = VoiceAnnouncer(this)
        
        viewModel = ViewModelProvider(this, object : ViewModelProvider.Factory {
            override fun <T : ViewModel> create(modelClass: Class<T>): T {
                return MainViewModel(bleManager, voiceAnnouncer) as T
            }
        })[MainViewModel::class.java]

        setContent {
            val permissions = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                listOf(
                    Manifest.permission.BLUETOOTH_SCAN,
                    Manifest.permission.BLUETOOTH_CONNECT
                )
            } else {
                listOf(
                    Manifest.permission.ACCESS_FINE_LOCATION
                )
            }
            
            val permissionsState = rememberMultiplePermissionsState(permissions = permissions)

            LaunchedEffect(Unit) {
                if (!permissionsState.allPermissionsGranted) {
                    permissionsState.launchMultiplePermissionRequest()
                }
            }

            MaterialTheme(colorScheme = darkColorScheme(
                background = Color(0xFF12121A),
                surface = Color(0xFF1E1E2C),
                primary = Color(0xFF00F2FE),
                secondary = Color(0xFF4FACFE)
            )) {
                Surface(modifier = Modifier.fillMaxSize()) {
                    val currentLang by viewModel.currentLanguage.collectAsState()
                    ProvideLocalizedContext(currentLang) {
                        if (permissionsState.allPermissionsGranted) {
                            CronografoApp(viewModel)
                        } else {
                            Box(contentAlignment = Alignment.Center, modifier = Modifier.fillMaxSize()) {
                                Text(stringResource(R.string.perm_required), color = Color.White)
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun ProvideLocalizedContext(languageCode: String, content: @Composable () -> Unit) {
    val context = LocalContext.current
    val locale = Locale(languageCode)
    val configuration = Configuration(context.resources.configuration)
    configuration.setLocale(locale)
    val localizedContext = context.createConfigurationContext(configuration)
    CompositionLocalProvider(LocalContext provides localizedContext) {
        content()
    }
}

@Composable
fun CronografoApp(viewModel: MainViewModel) {
    val isScanning by viewModel.isScanning.collectAsState()
    val isConnected by viewModel.isConnected.collectAsState()
    val statusText by viewModel.statusText.collectAsState()
    val devices by viewModel.devices.collectAsState()
    val shots by viewModel.shots.collectAsState()
    
    val lastShotNumber by viewModel.lastShotNumber.collectAsState()
    val lastShotVelocity by viewModel.lastShotVelocity.collectAsState()
    val lastShotEnergy by viewModel.lastShotEnergy.collectAsState()
    
    val avgVelocity by viewModel.avgVelocity.collectAsState()
    val minVelocity by viewModel.minVelocity.collectAsState()
    val maxVelocity by viewModel.maxVelocity.collectAsState()
    
    val avgEnergy by viewModel.avgEnergy.collectAsState()
    val minEnergy by viewModel.minEnergy.collectAsState()
    val maxEnergy by viewModel.maxEnergy.collectAsState()
    
    val pelletWeightText by viewModel.pelletWeightText.collectAsState()
    val currentLang by viewModel.currentLanguage.collectAsState()

    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        
        // Header
        Card(
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            shape = RoundedCornerShape(16.dp),
            modifier = Modifier.fillMaxWidth().padding(bottom = 16.dp)
        ) {
            Row(
                modifier = Modifier.padding(16.dp).fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column {
                    Text(stringResource(R.string.title_cronografo), color = MaterialTheme.colorScheme.primary, fontWeight = FontWeight.Bold, fontSize = 30.sp)
                    Text(statusText.asString(), color = Color.LightGray, fontSize = 24.sp)
                }
                Button(
                    onClick = { if (isScanning) viewModel.stopScan() else viewModel.startScan() },
                    colors = ButtonDefaults.buttonColors(containerColor = if (isScanning) Color.DarkGray else MaterialTheme.colorScheme.secondary)
                ) {
                    Text(if (isScanning) stringResource(R.string.btn_scanning) else stringResource(R.string.btn_scan), color = Color.White)
                }
            }
        }

        LazyColumn(verticalArrangement = Arrangement.spacedBy(16.dp)) {
            // Devices List (only if not connected)
            if (!isConnected) {
                item {
                    Text(stringResource(R.string.devices_available), color = Color.White, fontWeight = FontWeight.Bold)
                }
                items(devices) { device ->
                    @SuppressLint("MissingPermission")
                    val name = device.name ?: device.address
                    Card(
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Row(modifier = Modifier.padding(12.dp).fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                            Text(name, color = Color.White)
                            Button(onClick = { viewModel.connect(device) }) {
                                Text(stringResource(R.string.btn_connect))
                            }
                        }
                    }
                }
            }

            if (isConnected) {
                item {
                    Button(
                        onClick = { viewModel.disconnect() },
                        colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFFF4F58)),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(stringResource(R.string.btn_disconnect), color = Color.White)
                    }
                }

                // Hero Section
                item {
                    Card(
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                        shape = RoundedCornerShape(16.dp),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(modifier = Modifier.padding(24.dp).fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally) {
                            Text(lastShotNumber.asString(), color = Color.Gray, fontSize = 28.sp)
                            Row(verticalAlignment = Alignment.Bottom) {
                                Text(lastShotVelocity, color = MaterialTheme.colorScheme.primary, fontSize = 56.sp, fontWeight = FontWeight.Bold)
                                Spacer(modifier = Modifier.width(8.dp))
                                Text("m/s", color = MaterialTheme.colorScheme.primary, fontSize = 30.sp, modifier = Modifier.padding(bottom = 12.dp))
                            }
                            Text(stringResource(R.string.energy_label, lastShotEnergy), color = Color.White, fontSize = 28.sp)
                        }
                    }
                }

                // Stats Section
                item {
                    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(16.dp)) {
                        StatsCard(title = stringResource(R.string.title_velocity), avg = avgVelocity, min = minVelocity, max = maxVelocity, color = MaterialTheme.colorScheme.secondary, modifier = Modifier.weight(1f))
                        StatsCard(title = stringResource(R.string.title_energy), avg = avgEnergy, min = minEnergy, max = maxEnergy, color = MaterialTheme.colorScheme.primary, modifier = Modifier.weight(1f))
                    }
                }

                // Settings Section
                item {
                    Card(
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(modifier = Modifier.padding(16.dp)) {
                            Text(stringResource(R.string.settings_session), color = Color.White, fontWeight = FontWeight.Bold)
                            Spacer(modifier = Modifier.height(8.dp))
                            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween, modifier = Modifier.fillMaxWidth()) {
                                OutlinedTextField(
                                    value = pelletWeightText,
                                    onValueChange = { viewModel.updateWeightText(it) },
                                    label = { Text(stringResource(R.string.weight_label)) },
                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                    modifier = Modifier.weight(1f),
                                    singleLine = true
                                )
                                Spacer(modifier = Modifier.width(8.dp))
                                Button(onClick = { viewModel.sendWeight() }) {
                                    Text(stringResource(R.string.btn_send))
                                }
                                Spacer(modifier = Modifier.width(8.dp))
                                Button(onClick = { viewModel.resetSession() }, colors = ButtonDefaults.buttonColors(containerColor = Color.DarkGray)) {
                                    Text(stringResource(R.string.btn_reset))
                                }
                            }
                            
                            Spacer(modifier = Modifier.height(16.dp))
                            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.fillMaxWidth()) {
                                Text(stringResource(R.string.language_selector), color = Color.White, modifier = Modifier.weight(1f))
                                var expanded by remember { mutableStateOf(false) }
                                Box {
                                    Button(onClick = { expanded = true }, colors = ButtonDefaults.buttonColors(containerColor = Color.DarkGray)) {
                                        val langName = when (currentLang) {
                                            "ca" -> stringResource(R.string.lang_catalan)
                                            "en" -> stringResource(R.string.lang_english)
                                            else -> stringResource(R.string.lang_spanish)
                                        }
                                        Text(langName)
                                    }
                                    DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
                                        DropdownMenuItem(text = { Text(stringResource(R.string.lang_catalan)) }, onClick = { viewModel.setLanguage("ca"); expanded = false })
                                        DropdownMenuItem(text = { Text(stringResource(R.string.lang_spanish)) }, onClick = { viewModel.setLanguage("es"); expanded = false })
                                        DropdownMenuItem(text = { Text(stringResource(R.string.lang_english)) }, onClick = { viewModel.setLanguage("en"); expanded = false })
                                    }
                                }
                            }
                        }
                    }
                }

                // History Section
                item {
                    Text(stringResource(R.string.history_title), color = Color.White, fontWeight = FontWeight.Bold, fontSize = 24.sp)
                }
                
                items(shots) { shot ->
                    Row(
                        modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp).background(MaterialTheme.colorScheme.surface, RoundedCornerShape(8.dp)).padding(12.dp),
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text(stringResource(R.string.shot_number, shot.number.toString()), color = Color.White, fontWeight = FontWeight.Bold, fontSize = 24.sp, modifier = Modifier.weight(1f))
                        Text(shot.displayVelocity, color = MaterialTheme.colorScheme.primary, fontWeight = FontWeight.Bold, fontSize = 24.sp, modifier = Modifier.weight(1f))
                        Text(shot.displayEnergy, color = Color.LightGray, fontSize = 24.sp, modifier = Modifier.weight(1f))
                        Text(shot.displayTime, color = Color.Gray, fontSize = 24.sp, modifier = Modifier.weight(1f))
                    }
                }
            }
        }
    }
}

@Composable
fun StatsCard(title: String, avg: String, min: String, max: String, color: Color, modifier: Modifier = Modifier) {
    Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface), modifier = modifier) {
        Column(modifier = Modifier.padding(12.dp)) {
            Text(title, color = color, fontSize = 24.sp, fontWeight = FontWeight.Bold)
            Spacer(modifier = Modifier.height(8.dp))
            StatRow(stringResource(R.string.stat_avg), avg)
            StatRow(stringResource(R.string.stat_min), min)
            StatRow(stringResource(R.string.stat_max), max)
        }
    }
}

@Composable
fun StatRow(label: String, value: String) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(label, color = Color.Gray, fontSize = 24.sp)
        Text(value, color = Color.White, fontSize = 24.sp, fontWeight = FontWeight.Bold)
    }
}
