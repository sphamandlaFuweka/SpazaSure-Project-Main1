package com.spazasure.spazasure_app

import android.app.NotificationChannel
import android.app.NotificationManager
import android.os.Build
import android.os.Bundle
import io.flutter.embedding.android.FlutterActivity

class MainActivity : FlutterActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        createNotificationChannel()
    }

    /**
     * High-importance channel for push notifications, so they pop up on screen with a sound.
     * Its id matches default_notification_channel_id in AndroidManifest.xml and the channel
     * the backend sends to. Creating an existing channel again is a no-op.
     */
    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val channel = NotificationChannel(
            "spazasure_alerts",
            "Orders and account alerts",
            NotificationManager.IMPORTANCE_HIGH,
        ).apply {
            description = "New orders, deliveries, payments and account updates"
        }
        getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }
}
