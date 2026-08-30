Verification commands for the Roslyn DevSpace feature:

```powershell
dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj --filter Roslyn
dotnet test
dotnet build
dotnet publish src/DevBoard.csproj -c Release -r win-x64 --self-contained true
```
