dotnet publish -c ReleaseStripNoProfiling -r win10-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true
copy bin\ReleaseStripNoProfiling\net5.0\win10-x64\cyFight.blob bin\ReleaseStripNoProfiling\net5.0\win10-x64\publish\cyFight.blob
tar -c -f viroid.zip -C bin\ReleaseStripNoProfiling\net5.0\win10-x64\publish *
PAUSE