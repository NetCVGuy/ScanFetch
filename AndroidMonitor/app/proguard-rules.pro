# Add project specific ProGuard rules here.
-keepattributes Signature
-keepattributes *Annotation*
-keep class com.scanfetch.monitor.data.** { *; }
-dontwarn okhttp3.**
-dontwarn retrofit2.**
