# Build

```
dotnet publish -c Release -r win-x64 -p:SelfContained=true,PublishSingleFile=true,PublishReadyToRun=true,IncludeNativeLibrariesForSelfExtract=true,PublishTrimmed=false
```