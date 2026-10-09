@echo off

rem ============================================================

rem  AppClubCalculatoare - Instalare baza de date intr-un singur pas

rem ------------------------------------------------------------

rem  Folosire interactiva (dublu-click din folderul BD):

rem      Instaleaza_BD.bat

rem  Folosire din cmd (server si mod optional):

rem      Instaleaza_BD.bat [server] [NOU | UPDATE]

rem  Exemple:

rem      Instaleaza_BD.bat

rem      Instaleaza_BD.bat .\SQLEXPRESS NOU

rem      Instaleaza_BD.bat . UPDATE

rem

rem  Moduri:

rem    NOU     = instalare noua prin Instalare.sql, toate cele

rem              7 scripturi. ATENTIE: reseteaza datele demo!

rem    UPDATE  = actualizare sigura a unei baze existente, doar

rem              pasii 2-7, idempotenti: AuditLog, Autentificare,

rem              Permisiuni, MigrareFoto, Favorite, CalculatorCost.

rem              Pastreaza datele si pozele.

rem

rem  Fisier ASCII intentionat, fara diacritice, pentru afisare

rem  corecta pe orice codepage de consola. Flux pe goto, fara

rem  blocuri if imbricate.

rem ============================================================

setlocal

pushd "%~dp0"



set SERVER=.\SQLEXPRESS

if not "%~1"=="" set SERVER=%~1

set MOD=%~2

if "%MOD%"=="" goto PromptServer

if /I "%MOD%"=="NOU" goto CheckSqlcmd

if /I "%MOD%"=="UPDATE" goto CheckSqlcmd

echo Mod necunoscut: %MOD% - folositi NOU sau UPDATE.

goto SfarsitEroare



:PromptServer

echo.

echo Server curent: %SERVER%

set /p SERVER="Alt server - Enter pastreaza: "

set SERVER=%SERVER: =%

if "%SERVER%"=="" set SERVER=.\SQLEXPRESS



:CheckSqlcmd

where sqlcmd >nul 2>nul

if errorlevel 1 goto FaraSqlcmd



echo.

echo Test conexiune la %SERVER% ...

sqlcmd -S %SERVER% -Q "SELECT 1" -b >nul 2>nul <nul

if errorlevel 1 goto EroareConectare

echo Conectat.



set EXISTA=0

sqlcmd -S %SERVER% -d ClubCalculatoare -Q "SELECT 1" -b >nul 2>nul <nul

if not errorlevel 1 set EXISTA=1



if not "%MOD%"=="" goto ModAles

if "%EXISTA%"=="1" goto MeniuMod

echo Baza ClubCalculatoare nu exista - se face instalare NOUA.

set MOD=NOU

goto ModAles



:MeniuMod

echo.

echo Baza ClubCalculatoare EXISTA deja pe %SERVER%.

echo [R] REINSTALARE completa - reseteaza datele demo!

echo [A] ACTUALIZARE sigura - pasii 2-7, pastreaza datele.

set ALEGERE=A

set /p ALEGERE="Alegeti R/A - Enter este A: "

set ALEGERE=%ALEGERE: =%

if /I "%ALEGERE%"=="R" set MOD=NOU

if /I not "%ALEGERE%"=="R" set MOD=UPDATE



:ModAles

if /I "%MOD%"=="NOU" goto ModNou

goto ModUpdate



:ModNou

if "%EXISTA%"=="0" goto ExecNou

echo.

echo ATENTIE: reinstalarea sterge si re-creeaza tabelele cu

echo date demo - istoricul si pozele existente se pierd!

set CONFIRM=NU

set /p CONFIRM="Scrieti DA pentru confirmare: "

set CONFIRM=%CONFIRM: =%

if /I "%CONFIRM%"=="DA" goto ExecNou

echo Operatie anulata.

goto SfarsitOk



:ExecNou

echo.

echo === Instalare NOUA 1-7: Instalare.sql ===

sqlcmd -S %SERVER% -f 65001 -b -i Instalare.sql <nul

if errorlevel 1 goto EroareInstalare

goto Verificare



:ModUpdate

echo.

echo === Actualizare sigura 2-7 ===

call :RuleazaScript AuditLog.sql

if errorlevel 1 goto SfarsitEroare

call :RuleazaScript Autentificare_Update.sql

if errorlevel 1 goto SfarsitEroare

call :RuleazaScript Permisiuni.sql

if errorlevel 1 goto SfarsitEroare

call :RuleazaScript MigrareFoto.sql

if errorlevel 1 goto SfarsitEroare

call :RuleazaScript Favorite.sql

if errorlevel 1 goto SfarsitEroare

call :RuleazaScript CalculatorCost.sql

if errorlevel 1 goto SfarsitEroare

goto Verificare



:RuleazaScript

echo --- %1

sqlcmd -S %SERVER% -f 65001 -b -i "%1" <nul

if errorlevel 1 echo EROARE la %1 - instalare oprita.

exit /b %errorlevel%



:Verificare

echo.

echo === Verificare finala ===

sqlcmd -S %SERVER% -d ClubCalculatoare -Q "SET NOCOUNT ON; SELECT 'Echipamente='+CAST(COUNT(*) AS VARCHAR) FROM Echipamente; SELECT 'Utilizatori='+CAST(COUNT(*) AS VARCHAR) FROM Utilizatori; SELECT 'EchipamentFoto='+CAST(COUNT(*) AS VARCHAR) FROM EchipamentFoto; SELECT 'Favorite='+CAST(COUNT(*) AS VARCHAR) FROM Favorite; SELECT cod FROM Permisiuni WHERE cod LIKE 'FOTO[_]%%' OR cod='FAVORITE' OR cod='CALCULATOR_COST';" -b <nul

if errorlevel 1 goto EroareVerificare

echo.

echo GATA: baza ClubCalculatoare este la zi pe %SERVER%.

goto SfarsitOk



:FaraSqlcmd

echo EROARE: sqlcmd nu a fost gasit in PATH.

echo Instalati SQL Server Command Line Utilities sau rulati

echo scripturile manual din SSMS.

goto SfarsitEroare



:EroareConectare

echo EROARE: nu s-a putut conecta la %SERVER%.

echo Verificati: 1. SQL Server pornit, 2. numele instantei,

echo 3. autentificare Windows permisa.

goto SfarsitEroare



:EroareInstalare

echo EROARE la instalare - verificati mesajele de mai sus.

goto SfarsitEroare



:EroareVerificare

echo AVERTISMENT: verificarea a esuat - baza partiala?

goto SfarsitEroare



:SfarsitEroare

popd

pause

exit /b 1



:SfarsitOk

popd

pause

exit /b 0

