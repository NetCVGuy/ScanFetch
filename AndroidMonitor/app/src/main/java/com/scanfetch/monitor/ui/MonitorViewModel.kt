package com.scanfetch.monitor.ui

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.scanfetch.monitor.data.*
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

enum class ConnectionState {
    DISCONNECTED, CONNECTING, CONNECTED, ERROR
}

data class MonitorUiState(
    val connectionState: ConnectionState = ConnectionState.DISCONNECTED,
    val serverIp: String = "",
    val serverPort: String = "5000",
    val scanners: List<ScannerStatus> = emptyList(),
    val recentEvents: List<ScannerEvent> = emptyList(),
    val errorMessage: String? = null
)

class MonitorViewModel : ViewModel() {
    
    private val _uiState = MutableStateFlow(MonitorUiState())
    val uiState: StateFlow<MonitorUiState> = _uiState.asStateFlow()
    
    private var repository: ScanFetchRepository? = null
    
    fun updateServerIp(ip: String) {
        _uiState.value = _uiState.value.copy(serverIp = ip)
    }
    
    fun updateServerPort(port: String) {
        _uiState.value = _uiState.value.copy(serverPort = port)
    }
    
    fun connect() {
        val ip = _uiState.value.serverIp
        val port = _uiState.value.serverPort.toIntOrNull() ?: 5000
        
        if (ip.isBlank()) {
            _uiState.value = _uiState.value.copy(
                errorMessage = "Please enter server IP"
            )
            return
        }
        
        _uiState.value = _uiState.value.copy(
            connectionState = ConnectionState.CONNECTING,
            errorMessage = null
        )
        
        repository = ScanFetchRepository(ip, port)
        
        viewModelScope.launch {
            // Try to fetch initial status
            val result = repository?.getStatus()
            if (result?.isSuccess == true) {
                _uiState.value = _uiState.value.copy(
                    connectionState = ConnectionState.CONNECTED,
                    scanners = result.getOrNull()?.scanners ?: emptyList()
                )
                
                // Start periodic status updates
                startStatusPolling()
                
                // Fetch recent events
                fetchRecentEvents()
            } else {
                _uiState.value = _uiState.value.copy(
                    connectionState = ConnectionState.ERROR,
                    errorMessage = result?.exceptionOrNull()?.message ?: "Connection failed"
                )
            }
        }
    }
    
    fun disconnect() {
        repository = null
        _uiState.value = _uiState.value.copy(
            connectionState = ConnectionState.DISCONNECTED,
            scanners = emptyList(),
            recentEvents = emptyList()
        )
    }
    
    private fun startStatusPolling() {
        viewModelScope.launch {
            while (_uiState.value.connectionState == ConnectionState.CONNECTED) {
                val result = repository?.getStatus()
                if (result?.isSuccess == true) {
                    _uiState.value = _uiState.value.copy(
                        scanners = result.getOrNull()?.scanners ?: emptyList()
                    )
                } else {
                    _uiState.value = _uiState.value.copy(
                        connectionState = ConnectionState.ERROR,
                        errorMessage = "Lost connection to server"
                    )
                    break
                }
                delay(3000) // Poll every 3 seconds
            }
        }
    }
    
    private fun fetchRecentEvents() {
        viewModelScope.launch {
            val result = repository?.getErrors()
            if (result?.isSuccess == true) {
                _uiState.value = _uiState.value.copy(
                    recentEvents = result.getOrNull()?.errors ?: emptyList()
                )
            }
        }
    }
    
    fun refreshData() {
        if (_uiState.value.connectionState == ConnectionState.CONNECTED) {
            viewModelScope.launch {
                repository?.getStatus()?.let { result ->
                    if (result.isSuccess) {
                        _uiState.value = _uiState.value.copy(
                            scanners = result.getOrNull()?.scanners ?: emptyList()
                        )
                    }
                }
                
                repository?.getErrors()?.let { result ->
                    if (result.isSuccess) {
                        _uiState.value = _uiState.value.copy(
                            recentEvents = result.getOrNull()?.errors ?: emptyList()
                        )
                    }
                }
            }
        }
    }
}
