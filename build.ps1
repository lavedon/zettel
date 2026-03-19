dotnet publish -c Release --arch x64 --nologo --self-contained true
Move-Item -Path "C:\my-coding-projects\zettel\bin\Release\net10.0\win-x64\native\zettel.exe" -Destination "C:\tools\zettel.exe" -Force
