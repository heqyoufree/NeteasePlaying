rm $env:APPDATA\Rainmeter\Plugins\NeteasePlaying.dll
rm "C:\Program Files\Rainmeter\Newtonsoft.Json.dll"

cp PluginNeteasePlaying\x64\Debug\NeteasePlaying.dll $env:APPDATA\Rainmeter\Plugins
cp PluginNeteasePlaying\x64\Debug\Newtonsoft.Json.dll "C:\Program Files\Rainmeter"