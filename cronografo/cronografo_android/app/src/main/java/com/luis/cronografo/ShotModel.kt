package com.luis.cronografo

import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

data class ShotModel(
    val number: Int,
    val velocity: Float,
    val energy: Float,
    val timestamp: Long = System.currentTimeMillis()
) {
    val displayTime: String
        get() {
            val sdf = SimpleDateFormat("HH:mm:ss", Locale.getDefault())
            return sdf.format(Date(timestamp))
        }
        
    val displayVelocity: String
        get() = String.format(Locale.US, "%.1f m/s", velocity)
        
    val displayEnergy: String
        get() = String.format(Locale.US, "%.1f J", energy)
}
