@REM rd /S/Q System\Tomcat\work
@if "%OS%" == "Windows_NT"  goto start
@if "%SECOND_ENTRY%" == "yes" goto start
@set SECOND_ENTRY=no
command.com /E:8192 /C %0 %1 %2 %3 %4 %5 %6 %7 %8 %9
goto finish
:start

@call setEnv.bat

set JBOSS_HOME=%VIDA_HOME%jboss

set START_OPTION=%1

if "%START_OPTION%" == ""      GOTO runJBOSS
if "%START_OPTION%" == "start" GOTO runJBOSS
if "%START_OPTION%" == "stop"  call %JBOSS_HOME%\bin\jboss-cli.bat --connect :shutdown

GOTO finish

:runJBOSS


set JVM_FLAGS=%JVM_FLAGS% -XX:MaxPermSize=256m -Dfile.encoding=UTF-8 -Dapp.database.properties=com.ford.vcc.vida.data.jdbc.JBOSS7_database -Dvida.home=%VIDA_HOME%

set JAVA_OPTS=%JVM_FLAGS% -Xdebug -Xnoagent -Djava.compiler=NONE -Duser.language=en -Xrunjdwp:transport=dt_socket,address=4000,suspend=n,server=y 


%JAVA_HOME%\bin\java -jar -Dvida.home=%VIDA_HOME% %VIDA_HOME%eUpdate\eUpdate.jar installer

call %JBOSS_HOME%\bin\standalone.bat -c standalone.xml 


:finish

cd %VIDA_HOME%

@set SECOND_ENTRY=

