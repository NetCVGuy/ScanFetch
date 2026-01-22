package com.scanfetch.monitor.data

import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.sse.EventSource
import okhttp3.sse.EventSourceListener
import okhttp3.sse.EventSources
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.util.concurrent.TimeUnit
import com.google.gson.Gson

class ScanFetchRepository(private val serverIp: String, private val serverPort: Int) {
    
    private val baseUrl = "http://$serverIp:$serverPort"
    
    private val client = OkHttpClient.Builder()
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .writeTimeout(30, TimeUnit.SECONDS)
        .build()
    
    private val retrofit = Retrofit.Builder()
        .baseUrl(baseUrl)
        .client(client)
        .addConverterFactory(GsonConverterFactory.create())
        .build()
    
    private val api = retrofit.create(ScanFetchApi::class.java)
    private val gson = Gson()
    
    suspend fun getStatus(): Result<StatusResponse> {
        return try {
            val response = api.getStatus()
            if (response.isSuccessful && response.body() != null) {
                Result.success(response.body()!!)
            } else {
                Result.failure(Exception("Failed to get status: ${response.code()}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }
    
    suspend fun getErrors(): Result<ErrorsResponse> {
        return try {
            val response = api.getErrors()
            if (response.isSuccessful && response.body() != null) {
                Result.success(response.body()!!)
            } else {
                Result.failure(Exception("Failed to get errors: ${response.code()}"))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }
    
    fun connectEventStream(listener: EventSourceListener): EventSource {
        val request = Request.Builder()
            .url("$baseUrl/api/events")
            .header("Accept", "text/event-stream")
            .build()
        
        return EventSources.createFactory(client)
            .newEventSource(request, listener)
    }
}
