package com.spazasure.spazasure_app

import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import android.media.AudioAttributes
import android.media.AudioManager
import android.media.RingtoneManager
import android.os.Build
import android.os.Bundle
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {
    companion object {
        /**
         * Matches default_notification_channel_id in AndroidManifest.xml and the channel the
         * backend sends to (PushSender.cs). Android never lets an app change a channel's sound or
         * vibration once it exists, so turning vibration on needed a new id ("_v2").
         */
        const val CHANNEL_ID = "spazasure_alerts_v2"
        private const val OLD_CHANNEL_ID = "spazasure_alerts"
        private val VIBRATION = longArrayOf(0, 300, 150, 300)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        createNotificationChannel()
    }

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        // The in-app pop-up (app open) asks for the same tone and buzz a push gives
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "spazasure/alerts")
            .setMethodCallHandler { call, result ->
                if (call.method == "play") {
                    playAlert()
                    result.success(null)
                } else {
                    result.notImplemented()
                }
            }
    }

    /** High-importance channel: pushes pop up on screen, play the notification tone and vibrate. */
    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val manager = getSystemService(NotificationManager::class.java)
        val channel = NotificationChannel(
            CHANNEL_ID,
            "Orders and account alerts",
            NotificationManager.IMPORTANCE_HIGH,
        ).apply {
            description = "New orders, deliveries, payments and account updates"
            enableVibration(true)
            vibrationPattern = VIBRATION
            setSound(
                RingtoneManager.getDefaultUri(RingtoneManager.TYPE_NOTIFICATION),
                AudioAttributes.Builder()
                    .setUsage(AudioAttributes.USAGE_NOTIFICATION)
                    .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                    .build(),
            )
            enableLights(true)
        }
        manager.createNotificationChannel(channel)
        // The old channel had no vibration; remove it so the phone's settings show one channel
        manager.deleteNotificationChannel(OLD_CHANNEL_ID)
    }

    /** Notification tone and a short buzz, respecting the phone's silent and vibrate modes. */
    private fun playAlert() {
        val audio = getSystemService(Context.AUDIO_SERVICE) as AudioManager
        if (audio.ringerMode == AudioManager.RINGER_MODE_NORMAL) {
            RingtoneManager.getRingtone(
                applicationContext,
                RingtoneManager.getDefaultUri(RingtoneManager.TYPE_NOTIFICATION),
            )?.play()
        }
        if (audio.ringerMode != AudioManager.RINGER_MODE_SILENT) vibrate()
    }

    private fun vibrate() {
        val vibrator = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            (getSystemService(Context.VIBRATOR_MANAGER_SERVICE) as VibratorManager).defaultVibrator
        } else {
            @Suppress("DEPRECATION")
            getSystemService(Context.VIBRATOR_SERVICE) as Vibrator
        }
        if (!vibrator.hasVibrator()) return
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            vibrator.vibrate(VibrationEffect.createWaveform(VIBRATION, -1))
        } else {
            @Suppress("DEPRECATION")
            vibrator.vibrate(VIBRATION, -1)
        }
    }
}
