@echo off
setlocal EnableDelayedExpansion

set "dir=D:\Parsa Stuff\Visual Studio\Cherrisma"
set "apikey=AIzaSyDSWiUqR-6jXWJyLk0qXG5VyMCOUSkv_lg"

echo Enter your query:
set /p "query=> "

:: Validate directory
if not exist "%dir%" (
    echo Error: Directory "%dir%" does not exist.
    pause
    exit /b 1
)

:: Validate query
if "!query!"=="" (
    echo Error: Query cannot be empty.
    pause
    exit /b 1
)

:: Validate API key
if "!apikey!"=="" (
    echo Error: API key is not set.
    pause
    exit /b 1
)

:: Run ScopeScout with hardcoded extensions
scopescout "%dir%" "!query!" "%apikey%" --print-gemini-response --extensions=yaml,cs

if errorlevel 1 (
    echo Error: ScopeScout execution failed. Check the directory, API key, or query.
)

pause