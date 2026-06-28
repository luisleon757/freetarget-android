package com.luis.cronografo.tts

import android.content.Context
import android.content.res.Configuration
import android.speech.tts.TextToSpeech
import android.util.Log
import com.luis.cronografo.R
import java.util.Locale
import kotlin.math.roundToInt

class VoiceAnnouncer(private val baseContext: Context) : TextToSpeech.OnInitListener {
    private var tts: TextToSpeech = TextToSpeech(baseContext, this)
    private var isInitialized = false
    private var currentLocale: Locale = Locale("ca")
    private var localizedContext: Context = baseContext

    override fun onInit(status: Int) {
        if (status == TextToSpeech.SUCCESS) {
            setLanguage("ca") // Default to Catalan
            val result = tts.setLanguage(currentLocale)
            if (result == TextToSpeech.LANG_MISSING_DATA || result == TextToSpeech.LANG_NOT_SUPPORTED) {
                Log.e("VoiceAnnouncer", "Language not supported")
            } else {
                isInitialized = true
            }
        } else {
            Log.e("VoiceAnnouncer", "Error initializing TTS")
        }
    }

    fun setLanguage(langCode: String) {
        currentLocale = Locale(langCode)
        if (isInitialized) {
            tts.language = currentLocale
        }
        val config = Configuration(baseContext.resources.configuration)
        config.setLocale(currentLocale)
        localizedContext = baseContext.createConfigurationContext(config)
    }

    private fun formatWithDecimal(value: Float): String {
        val roundedValue = (value * 10).roundToInt()
        val intPart = roundedValue / 10
        val decPart = roundedValue % 10
        return if (decPart > 0) {
            localizedContext.getString(R.string.voice_decimal_con, intPart.toString(), decPart.toString())
        } else {
            "$intPart"
        }
    }

    fun announceShot(velocity: Float, energy: Float) {
        if (isInitialized) {
            val velStr = formatWithDecimal(velocity)
            val enStr = formatWithDecimal(energy)
            val text = localizedContext.getString(R.string.voice_shot_info, velStr, enStr)
            tts.speak(text, TextToSpeech.QUEUE_FLUSH, null, "ShotAnnouncement")
        }
    }

    fun shutdown() {
        tts.stop()
        tts.shutdown()
    }
}
