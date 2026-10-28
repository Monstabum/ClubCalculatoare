@echo off
rem ============================================================
rem  AppClubCalculatoare - pregatire baza de date (dublu-click)
rem ------------------------------------------------------------
rem  Ruleaza singur, fara intrebari:
rem    - baza lipseste  -> INSTALARE completa (Instalare.sql)
rem    - baza exista    -> ACTUALIZARE sigura, datele se pastreaza
rem
rem  Server implicit: .\SQLEXPRESS (ca in App.config).
rem  Optional, din cmd:
rem    Instaleaza_BD.bat NOU          reinstalare completa
rem    Instaleaza_BD.bat SERVER       alt server
rem    Instaleaza_BD.bat SERVER NOU   alt server + reinstalare
rem
rem  Log tehnic complet: bd_log.txt, langa acest fisier.
rem  Fisier ASCII intentionat (fara diacritice).
rem ============================================================

setlocal
pushd "%~dp0"

set SERVER=.\SQLEXPRESS
set MOD=AUTO
if /I "%~1"=="NOU" set MOD=NOU
if /I "%~1"=="REINSTALARE" set MOD=NOU
if not "%~1"=="" if /I not "%~1"=="NOU" if /I not "%~1"=="REINSTALARE" set SERVER=%~1
if /I "%~2"=="NOU" set MOD=NOU

echo ============================================================
echo  AppClubCalculatoare - pregatire baza de date
echo ============================================================
echo  Server: %SERVER%
echo.

where sqlcmd >nul 2>nul
if errorlevel 1 goto FaraSqlcmd

echo  Test conexiune ...
sqlcmd -S %SERVER% -Q "SELECT 1" -b >nul 2>nul <nul
if errorlevel 1 goto EroareConectare
echo  Conectat.
echo.

set EXISTA=0
sqlcmd -S %SERVER% -d ClubCalculatoare -Q "SELECT 1" -b >nul 2>nul <nul
if not errorlevel 1 set EXISTA=1

> "bd_log.txt" echo === AppClubCalculatoare - rulare %DATE% %TIME% ===

if "%MOD%"=="NOU" goto ModNou
if "%EXISTA%"=="1" goto ExecUpdate
goto ExecNou

:ModNou
if "%EXISTA%"=="1" echo  ATENTIE: mod NOU - datele demo existente se reseteaza.
goto ExecNou

:ExecNou
echo  INSTALARE COMPLETA (pasii 1-10). Poate dura pana la un minut ...
echo.
echo --- Instalare.sql >> "bd_log.txt"
sqlcmd -S %SERVER% -f 65001 -b -i Instalare.sql >> "bd_log.txt" 2>&1 <nul
if errorlevel 1 goto EroareInstalare
goto Verificare

:ExecUpdate
echo  ACTUALIZARE SIGURA (pasii 2-10). Datele se pastreaza.
echo.
set NR_EROARE=0
call :RuleazaScript AuditLog.sql
call :RuleazaScript Autentificare_Update.sql
call :RuleazaScript Permisiuni.sql
call :RuleazaScript MigrareFoto.sql
call :RuleazaScript Favorite.sql
call :RuleazaScript CalculatorCost.sql
call :RuleazaScript Adauga_DurataMinute.sql
call :RuleazaScript 10_Performance_Indexes.sql
call :RuleazaScript 20_usp_Dashboard.sql
if not "%NR_EROARE%"=="0" goto EroareUpdate
goto Verificare

:RuleazaScript
echo --- %1 >> "bd_log.txt"
sqlcmd -S %SERVER% -d ClubCalculatoare -f 65001 -b -i "%1" >> "bd_log.txt" 2>&1 <nul
if errorlevel 1 (
    echo   [EROARE] %1
    set /a NR_EROARE=NR_EROARE+1
) else (
    echo   [OK]     %1
)
exit /b 0

:Verificare
echo === Verificare finala ===
sqlcmd -S %SERVER% -d ClubCalculatoare -b -h -1 -W -Q "SET NOCOUNT ON; SELECT 'Echipamente='+CAST((SELECT COUNT(*) FROM Echipamente) AS VARCHAR(10)) + '  Utilizatori='+CAST((SELECT COUNT(*) FROM Utilizatori) AS VARCHAR(10)) + '  EchipamentFoto='+CAST((SELECT COUNT(*) FROM EchipamentFoto) AS VARCHAR(10)) + '  Favorite='+CAST((SELECT COUNT(*) FROM Favorite) AS VARCHAR(10));" <nul
if errorlevel 1 goto EroareVerificare

echo.
echo ============================================================
echo  GATA: baza ClubCalculatoare este pregatita pe %SERVER%.
echo ============================================================
echo.
echo  Conturi:  Constantin / 121212  (Administrator)
echo            Maxim / 575757       (Utilizator)
goto SfarsitOk

:FaraSqlcmd
echo.
echo  EROARE: sqlcmd nu a fost gasit in PATH.
echo  Instalati SQL Server Client Tools sau rulati scripturile
echo  manual din SSMS (vezi INSTRUCTIUNI.txt).
goto SfarsitEroare

:EroareConectare
echo.
echo  EROARE: nu s-a putut conecta la %SERVER%.
echo  Verificati: 1) serviciul SQL Server pornit, 2) numele
echo  instantei, 3) autentificarea Windows permisa.
goto SfarsitEroare

:EroareInstalare
echo.
echo  EROARE la instalare - detalii in bd_log.txt.
goto SfarsitEroare

:EroareUpdate
echo.
echo  EROARE la cel putin un script - detalii in bd_log.txt.
goto SfarsitEroare

:EroareVerificare
echo.
echo  ATENTIE: verificarea finala a esuat - baza incompleta?
echo  Detalii in bd_log.txt.
goto SfarsitEroare

:SfarsitEroare
popd
echo.
pause
exit /b 1

:SfarsitOk
popd
echo.
pause
exit /b 0
