package com.scanfetch.monitor

import android.app.Application
import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import android.os.Build

class MonitorApplication : Application() {
    
    companion object {
        const val NOTIFICATION_CHANNEL_ID = "scanner_alerts"
        const val SERVICE_NOTIFICATION_CHANNEL_ID = "monitor_service"
    }
    
    override fun onCreate() {
        super.onCreate()
        createNotificationChannels()
    }
    
    private fun createNotificationChannels() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val notificationManager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
            
            // Alert channel for scanner errors
            val alertChannel = NotificationChannel(
                NOTIFICATION_CHANNEL_ID,
                getString(R.string.notification_channel_name),
                NotificationManager.IMPORTANCE_HIGH
            ).apply {
                description = getString(R.string.notification_channel_desc)
                enableVibration(true)
                enableLights(true)
            }
            
            // Service channel for foreground service
            val serviceChannel = NotificationChannel(
                SERVICE_NOTIFICATION_CHANNEL_ID,
                "Monitor Service",
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = "Shows when monitoring is active"
            }
            
            notificationManager.createNotificationChannel(alertChannel)
            notificationManager.createNotificationChannel(serviceChannel)
        }
    }
}
