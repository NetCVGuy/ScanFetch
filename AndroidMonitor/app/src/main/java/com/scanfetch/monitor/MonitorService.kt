package com.scanfetch.monitor

import android.app.*
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat
import com.google.gson.Gson
import com.scanfetch.monitor.data.ScanFetchRepository
import com.scanfetch.monitor.data.ScannerEvent
import kotlinx.coroutines.*
import okhttp3.Response
import okhttp3.sse.EventSource
import okhttp3.sse.EventSourceListener
import java.util.concurrent.atomic.AtomicBoolean

class MonitorService : Service() {
    
    private val serviceScope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private var repository: ScanFetchRepository? = null
    private var eventSource: EventSource? = null
    private val isRunning = AtomicBoolean(false)
    private val gson = Gson()
    
    companion object {
        const val ACTION_START = "com.scanfetch.monitor.START"
        const val ACTION_STOP = "com.scanfetch.monitor.STOP"
        const val EXTRA_SERVER_IP = "server_ip"
        const val EXTRA_SERVER_PORT = "server_port"
        private const val NOTIFICATION_ID = 1001
        
        fun start(context: Context, serverIp: String, serverPort: Int) {
            val intent = Intent(context, MonitorService::class.java).apply {
                action = ACTION_START
                putExtra(EXTRA_SERVER_IP, serverIp)
                putExtra(EXTRA_SERVER_PORT, serverPort)
            }
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(intent)
            } else {
                context.startService(intent)
            }
        }
        
        fun stop(context: Context) {
            val intent = Intent(context, MonitorService::class.java).apply {
                action = ACTION_STOP
            }
            context.startService(intent)
        }
    }
    
    override fun onBind(intent: Intent?): IBinder? = null
    
    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_START -> {
                val serverIp = intent.getStringExtra(EXTRA_SERVER_IP) ?: return START_NOT_STICKY
                val serverPort = intent.getIntExtra(EXTRA_SERVER_PORT, 5000)
                startMonitoring(serverIp, serverPort)
            }
            ACTION_STOP -> {
                stopMonitoring()
            }
        }
        return START_STICKY
    }
    
    private fun startMonitoring(serverIp: String, serverPort: Int) {
        if (isRunning.get()) return
        
        startForeground(NOTIFICATION_ID, createServiceNotification())
        isRunning.set(true)
        
        repository = ScanFetchRepository(serverIp, serverPort)
        
        eventSource = repository?.connectEventStream(object : EventSourceListener() {
            override fun onOpen(eventSource: EventSource, response: Response) {
                // Connected to SSE stream
            }
            
            override fun onEvent(
                eventSource: EventSource,
                id: String?,
                type: String?,
                data: String
            ) {
                try {
                    val event = gson.fromJson(data, ScannerEvent::class.java)
                    
                    // Show notification for errors and disconnections
                    if (event.type == "ScannerError" || event.type == "ScannerDisconnected") {
                        showAlertNotification(event)
                    }
                } catch (e: Exception) {
                    e.printStackTrace()
                }
            }
            
            override fun onClosed(eventSource: EventSource) {
                // Connection closed
            }
            
            override fun onFailure(eventSource: EventSource, t: Throwable?, response: Response?) {
                t?.printStackTrace()
                // Try to reconnect after delay
                serviceScope.launch {
                    delay(5000)
                    if (isRunning.get() && repository != null) {
                        this@MonitorService.eventSource = repository?.connectEventStream(this@object)
                    }
                }
            }
        })
    }
    
    private fun stopMonitoring() {
        isRunning.set(false)
        eventSource?.cancel()
        eventSource = null
        repository = null
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
    }
    
    private fun createServiceNotification(): Notification {
        val intent = Intent(this, MainActivity::class.java)
        val pendingIntent = PendingIntent.getActivity(
            this, 0, intent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )
        
        return NotificationCompat.Builder(this, MonitorApplication.SERVICE_NOTIFICATION_CHANNEL_ID)
            .setContentTitle(getString(R.string.service_notification_title))
            .setContentText(getString(R.string.service_notification_text))
            .setSmallIcon(android.R.drawable.ic_menu_info_details)
            .setContentIntent(pendingIntent)
            .setOngoing(true)
            .build()
    }
    
    private fun showAlertNotification(event: ScannerEvent) {
        val notificationManager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        
        val title = when (event.type) {
            "ScannerError" -> "Scanner Error"
            "ScannerDisconnected" -> "Scanner Disconnected"
            else -> "Scanner Alert"
        }
        
        val text = "${event.scanner ?: "Unknown"}: ${event.message}"
        
        val intent = Intent(this, MainActivity::class.java)
        val pendingIntent = PendingIntent.getActivity(
            this, 0, intent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )
        
        val notification = NotificationCompat.Builder(this, MonitorApplication.NOTIFICATION_CHANNEL_ID)
            .setContentTitle(title)
            .setContentText(text)
            .setSmallIcon(android.R.drawable.stat_notify_error)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setAutoCancel(true)
            .setContentIntent(pendingIntent)
            .build()
        
        notificationManager.notify(System.currentTimeMillis().toInt(), notification)
    }
    
    override fun onDestroy() {
        super.onDestroy()
        stopMonitoring()
        serviceScope.cancel()
    }
}
