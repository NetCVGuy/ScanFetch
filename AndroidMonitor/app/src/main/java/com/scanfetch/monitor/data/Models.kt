package com.scanfetch.monitor.data

import com.google.gson.annotations.SerializedName

data class ScannerStatus(
    @SerializedName("name") val name: String,
    @SerializedName("enabled") val enabled: Boolean,
    @SerializedName("connected") val connected: Boolean,
    @SerializedName("role") val role: String,
    @SerializedName("ip") val ip: String,
    @SerializedName("port") val port: Int,
    @SerializedName("remoteEndpoint") val remoteEndpoint: String?
)

data class ScannerEvent(
    @SerializedName("type") val type: String,
    @SerializedName("scanner") val scanner: String?,
    @SerializedName("message") val message: String,
    @SerializedName("timestamp") val timestamp: String,
    @SerializedName("remote") val remote: String?,
    @SerializedName("details") val details: String?
)

data class StatusResponse(
    @SerializedName("status") val status: String,
    @SerializedName("scanners") val scanners: List<ScannerStatus>
)

data class ErrorsResponse(
    @SerializedName("errors") val errors: List<ScannerEvent>
)

data class HistoryResponse(
    @SerializedName("history") val history: List<ScannerEvent>
)
