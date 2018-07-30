# trackme
A quick and dirty prototype for a tool that monitors your written or spoken input to measure the sentiment of what you are writing.

It uses Azure's speech to text api for understanding what you dictate and then LUIS to parse the text on a sentence by sentence basis, 
thus deriving a simple value that gives an indication of the mood of your text

it is only a quick prototype for a competition some time ago, so do not expect clean code or anything. however it could easily be refined for example as a plugin for office, so that when you are writing an email or a text it could be watching over the general feeling of the text.

it uses classic Windows hooks (as a keylogger) to intercept your written input and send it to LUIS for analysis in the background, everytime 
a sentence is finished (which is everytime a dot is written, for simplicity's sake). Use at your own risk.

if interested just visit the \_doc folder for some screenshots and the "architecture" diagram...

API keys for LUIS not included.
