package com.scanfetch.monitor

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import androidx.lifecycle.viewmodel.compose.viewModel
import com.scanfetch.monitor.data.ScannerEvent
import com.scanfetch.monitor.data.ScannerStatus
import com.scanfetch.monitor.ui.ConnectionState
import com.scanfetch.monitor.ui.MonitorViewModel

class MainActivity : ComponentActivity() {
    
    private val notificationPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { isGranted ->
        // Handle permission result
    }
    
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        // Request notification permission on Android 13+
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(
                    this,
                    Manifest.permission.POST_NOTIFICATIONS
                ) != PackageManager.PERMISSION_GRANTED
            ) {
                notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
            }
        }
        
        setContent {
            MaterialTheme(
                colorScheme = darkColorScheme(
                    primary = Color(0xFF2196F3),
                    secondary = Color(0xFF03DAC5),
                    background = Color(0xFF121212),
                    surface = Color(0xFF1E1E1E),
                    error = Color(0xFFCF6679)
                )
            ) {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    MonitorScreen()
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MonitorScreen(viewModel: MonitorViewModel = viewModel()) {
    val uiState by viewModel.uiState.collectAsState()
    
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("ScanFetch Monitor") },
                actions = {
                    if (uiState.connectionState == ConnectionState.CONNECTED) {
                        IconButton(onClick = { viewModel.refreshData() }) {
                            Icon(Icons.Default.Refresh, contentDescription = "Refresh")
                        }
                    }
                }
            )
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(16.dp)
        ) {
            // Connection card
            ConnectionCard(
                serverIp = uiState.serverIp,
                serverPort = uiState.serverPort,
                connectionState = uiState.connectionState,
                errorMessage = uiState.errorMessage,
                onIpChange = viewModel::updateServerIp,
                onPortChange = viewModel::updateServerPort,
                onConnect = viewModel::connect,
                onDisconnect = viewModel::disconnect
            )
            
            Spacer(modifier = Modifier.height(16.dp))
            
            if (uiState.connectionState == ConnectionState.CONNECTED) {
                // Scanners status
                Text(
                    text = "Scanners Status",
                    fontSize = 20.sp,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.padding(bottom = 8.dp)
                )
                
                LazyColumn(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    items(uiState.scanners) { scanner ->
                        ScannerCard(scanner)
                    }
                    
                    if (uiState.recentEvents.isNotEmpty()) {
                        item {
                            Spacer(modifier = Modifier.height(8.dp))
                            Text(
                                text = "Recent Events",
                                fontSize = 20.sp,
                                fontWeight = FontWeight.Bold,
                                modifier = Modifier.padding(vertical = 8.dp)
                            )
                        }
                        
                        items(uiState.recentEvents) { event ->
                            EventCard(event)
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun ConnectionCard(
    serverIp: String,
    serverPort: String,
    connectionState: ConnectionState,
    errorMessage: String?,
    onIpChange: (String) -> Unit,
    onPortChange: (String) -> Unit,
    onConnect: () -> Unit,
    onDisconnect: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        )
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            OutlinedTextField(
                value = serverIp,
                onValueChange = onIpChange,
                label = { Text("Server IP") },
                placeholder = { Text("192.168.1.100") },
                enabled = connectionState == ConnectionState.DISCONNECTED,
                modifier = Modifier.fillMaxWidth()
            )
            
            Spacer(modifier = Modifier.height(8.dp))
            
            OutlinedTextField(
                value = serverPort,
                onValueChange = onPortChange,
                label = { Text("Port") },
                placeholder = { Text("5000") },
                enabled = connectionState == ConnectionState.DISCONNECTED,
                modifier = Modifier.fillMaxWidth()
            )
            
            Spacer(modifier = Modifier.height(16.dp))
            
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Status indicator
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        modifier = Modifier
                            .size(12.dp)
                            .background(
                                color = when (connectionState) {
                                    ConnectionState.CONNECTED -> Color.Green
                                    ConnectionState.CONNECTING -> Color.Yellow
                                    ConnectionState.ERROR -> Color.Red
                                    ConnectionState.DISCONNECTED -> Color.Gray
                                },
                                shape = RoundedCornerShape(6.dp)
                            )
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = when (connectionState) {
                            ConnectionState.CONNECTED -> "Connected"
                            ConnectionState.CONNECTING -> "Connecting..."
                            ConnectionState.ERROR -> "Error"
                            ConnectionState.DISCONNECTED -> "Disconnected"
                        }
                    )
                }
                
                // Connect/Disconnect button
                Button(
                    onClick = {
                        if (connectionState == ConnectionState.CONNECTED) {
                            onDisconnect()
                        } else {
                            onConnect()
                        }
                    },
                    enabled = connectionState != ConnectionState.CONNECTING
                ) {
                    Text(
                        if (connectionState == ConnectionState.CONNECTED) "Disconnect" else "Connect"
                    )
                }
            }
            
            if (errorMessage != null) {
                Spacer(modifier = Modifier.height(8.dp))
                Text(
                    text = errorMessage,
                    color = MaterialTheme.colorScheme.error,
                    fontSize = 14.sp
                )
            }
        }
    }
}

@Composable
fun ScannerCard(scanner: ScannerStatus) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = scanner.name,
                    fontSize = 18.sp,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = "${scanner.role} - ${scanner.ip}:${scanner.port}",
                    fontSize = 14.sp,
                    color = Color.Gray
                )
                if (scanner.remoteEndpoint != null) {
                    Text(
                        text = "Remote: ${scanner.remoteEndpoint}",
                        fontSize = 12.sp,
                        color = Color.Gray
                    )
                }
            }
            
            Icon(
                imageVector = if (scanner.connected) Icons.Default.CheckCircle else Icons.Default.Cancel,
                contentDescription = if (scanner.connected) "Connected" else "Disconnected",
                tint = if (scanner.connected) Color.Green else Color.Red,
                modifier = Modifier.size(32.dp)
            )
        }
    }
}

@Composable
fun EventCard(event: ScannerEvent) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = when (event.type) {
                "ScannerError" -> Color(0xFF4A0E0E)
                "ScannerDisconnected" -> Color(0xFF4A2E0E)
                else -> MaterialTheme.colorScheme.surface
            }
        )
    ) {
        Column(modifier = Modifier.padding(12.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    text = event.type,
                    fontSize = 16.sp,
                    fontWeight = FontWeight.Bold,
                    color = when (event.type) {
                        "ScannerError" -> Color(0xFFFF6B6B)
                        "ScannerDisconnected" -> Color(0xFFFFAA6B)
                        else -> Color.White
                    }
                )
                Text(
                    text = event.timestamp,
                    fontSize = 12.sp,
                    color = Color.Gray
                )
            }
            
            Spacer(modifier = Modifier.height(4.dp))
            
            if (event.scanner != null) {
                Text(
                    text = "Scanner: ${event.scanner}",
                    fontSize = 14.sp,
                    color = Color.LightGray
                )
            }
            
            Text(
                text = event.message,
                fontSize = 14.sp,
                color = Color.White
            )
            
            if (event.details != null) {
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = event.details,
                    fontSize = 12.sp,
                    color = Color.Gray,
                    maxLines = 3
                )
            }
        }
    }
}
