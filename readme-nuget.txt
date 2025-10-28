/* Sample nuget CLI */
nuget update -Self
.\nuget pack Verifone.ECRTerminal\Verifone.ECRTerminal.csproj -Build -Symbols -Properties Configuration=Release
.\nuget push Verifone.ECRTerminal.1.0.0.nupkg -Source "rmsoftpackages" -ApiKey apikey
