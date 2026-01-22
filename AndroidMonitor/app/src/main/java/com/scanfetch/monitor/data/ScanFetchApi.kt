package com.scanfetch.monitor.data

import retrofit2.Response
import retrofit2.http.GET
import retrofit2.http.Query

interface ScanFetchApi {
    @GET("/api/status")
    suspend fun getStatus(): Response<StatusResponse>

    @GET("/api/errors")
    suspend fun getErrors(@Query("count") count: Int = 50): Response<ErrorsResponse>

    @GET("/api/history")
    suspend fun getHistory(@Query("count") count: Int = 50): Response<HistoryResponse>
}
