"""
MCP Server для автоматизации тестирования приложения AppClubCalculatoare.
Использует pyautogui для управления мышкой и клавиатурой.

Запуск:
    python server.py
"""

import asyncio
import base64
import io
import os
import time
from typing import Optional

import pyautogui
from mcp.server import Server
from mcp.server.stdio import stdio_server
from mcp.types import Tool, TextContent, ImageContent

# Отключаем fail-safe для pyautogui
pyautogui.FAILSAFE = False

# Создаем MCP-сервер
server = Server("desktop-automation")


# ── Инструменты ──────────────────────────────────────────────────

def get_tools() -> list[Tool]:
    """Возвращает список доступных инструментов."""
    return [
        Tool(
            name="click",
            description="Клик левой кнопкой мыши по указанным координатам",
            inputSchema={
                "type": "object",
                "properties": {
                    "x": {"type": "number", "description": "X-координата"},
                    "y": {"type": "number", "description": "Y-координата"},
                },
                "required": ["x", "y"],
            },
        ),
        Tool(
            name="double_click",
            description="Двойной клик левой кнопкой мыши",
            inputSchema={
                "type": "object",
                "properties": {
                    "x": {"type": "number", "description": "X-координата"},
                    "y": {"type": "number", "description": "Y-координата"},
                },
                "required": ["x", "y"],
            },
        ),
        Tool(
            name="right_click",
            description="Клик правой кнопкой мыши",
            inputSchema={
                "type": "object",
                "properties": {
                    "x": {"type": "number", "description": "X-координата"},
                    "y": {"type": "number", "description": "Y-координата"},
                },
                "required": ["x", "y"],
            },
        ),
        Tool(
            name="move_mouse",
            description="Перемещение мыши к указанным координатам",
            inputSchema={
                "type": "object",
                "properties": {
                    "x": {"type": "number", "description": "X-координата"},
                    "y": {"type": "number", "description": "Y-координата"},
                },
                "required": ["x", "y"],
            },
        ),
        Tool(
            name="type_text",
            description="Ввод текста с клавиатуры",
            inputSchema={
                "type": "object",
                "properties": {
                    "text": {"type": "string", "description": "Текст для ввода"},
                    "interval": {
                        "type": "number",
                        "description": "Интервал между символами (секунды)",
                        "default": 0.05,
                    },
                },
                "required": ["text"],
            },
        ),
        Tool(
            name="hotkey",
            description="Нажатие комбинации клавиш (например, 'ctrl', 'c')",
            inputSchema={
                "type": "object",
                "properties": {
                    "keys": {
                        "type": "array",
                        "items": {"type": "string"},
                        "description": "Список клавиш для нажатия",
                    },
                },
                "required": ["keys"],
            },
        ),
        Tool(
            name="press_key",
            description="Нажатие одной клавиши",
            inputSchema={
                "type": "object",
                "properties": {
                    "key": {
                        "type": "string",
                        "description": "Клавиша (например, 'enter', 'tab', 'escape')",
                    },
                    "presses": {
                        "type": "number",
                        "description": "Количество нажатий",
                        "default": 1,
                    },
                },
                "required": ["key"],
            },
        ),
        Tool(
            name="screenshot",
            description="Сделать скриншот экрана и вернуть его как base64",
            inputSchema={
                "type": "object",
                "properties": {
                    "save_path": {
                        "type": "string",
                        "description": "Путь для сохранения скриншота (опционально)",
                    },
                },
            },
        ),
        Tool(
            name="scroll",
            description="Прокрутка колеса мыши",
            inputSchema={
                "type": "object",
                "properties": {
                    "clicks": {
                        "type": "number",
                        "description": "Количество щелчков колеса (отрицательное = вниз)",
                    },
                    "x": {"type": "number", "description": "X-координата (опционально)"},
                    "y": {"type": "number", "description": "Y-координата (опционально)"},
                },
                "required": ["clicks"],
            },
        ),
        Tool(
            name="find_and_click",
            description="Найти элемент на экране по изображению и кликнуть по нему",
            inputSchema={
                "type": "object",
                "properties": {
                    "image_path": {
                        "type": "string",
                        "description": "Путь к изображению элемента",
                    },
                    "confidence": {
                        "type": "number",
                        "description": "Уровень уверенности (0.0-1.0)",
                        "default": 0.9,
                    },
                },
                "required": ["image_path"],
            },
        ),
        Tool(
            name="wait",
            description="Ожидание указанного количества секунд",
            inputSchema={
                "type": "object",
                "properties": {
                    "seconds": {
                        "type": "number",
                        "description": "Количество секунд ожидания",
                    },
                },
                "required": ["seconds"],
            },
        ),
        Tool(
            name="get_mouse_position",
            description="Получить текущую позицию курсора мыши",
            inputSchema={"type": "object", "properties": {}},
        ),
        Tool(
            name="get_screen_size",
            description="Получить размер экрана",
            inputSchema={"type": "object", "properties": {}},
        ),
        Tool(
            name="run_test_scenario",
            description="Запустить тестовый сценарий для приложения AppClubCalculatoare",
            inputSchema={
                "type": "object",
                "properties": {
                    "scenario": {
                        "type": "string",
                        "description": "Название сценария (login, crud, reports, dashboard)",
                    },
                },
                "required": ["scenario"],
            },
        ),
    ]


async def handle_call_tool(name: str, arguments: dict) -> list:
    """Выполняет указанный инструмент."""
    
    if name == "click":
        x, y = arguments["x"], arguments["y"]
        pyautogui.click(x, y)
        return [TextContent(type="text", text=f"Клик выполнен по координатам ({x}, {y})")]
    
    elif name == "double_click":
        x, y = arguments["x"], arguments["y"]
        pyautogui.doubleClick(x, y)
        return [TextContent(type="text", text=f"Двойной клик выполнен по координатам ({x}, {y})")]
    
    elif name == "right_click":
        x, y = arguments["x"], arguments["y"]
        pyautogui.rightClick(x, y)
        return [TextContent(type="text", text=f"Правый клик выполнен по координатам ({x}, {y})")]
    
    elif name == "move_mouse":
        x, y = arguments["x"], arguments["y"]
        pyautogui.moveTo(x, y)
        return [TextContent(type="text", text=f"Мышь перемещена к ({x}, {y})")]
    
    elif name == "type_text":
        text = arguments["text"]
        interval = arguments.get("interval", 0.05)
        pyautogui.typewrite(text, interval=interval)
        return [TextContent(type="text", text=f"Текст введен: '{text}'")]
    
    elif name == "hotkey":
        keys = arguments["keys"]
        pyautogui.hotkey(*keys)
        return [TextContent(type="text", text=f"Комбинация клавиш нажата: {' + '.join(keys)}")]
    
    elif name == "press_key":
        key = arguments["key"]
        presses = arguments.get("presses", 1)
        pyautogui.press(key, presses=presses)
        return [TextContent(type="text", text=f"Клавиша '{key}' нажата {presses} раз(а)")]
    
    elif name == "screenshot":
        save_path = arguments.get("save_path")
        
        # Делаем скриншот
        screenshot = pyautogui.screenshot()
        
        # Сохраняем в файл если указан путь
        if save_path:
            screenshot.save(save_path)
        
        # Конвертируем в base64
        buffered = io.BytesIO()
        screenshot.save(buffered, format="PNG")
        img_str = base64.b64encode(buffered.getvalue()).decode()
        
        return [
            ImageContent(
                type="image",
                data=img_str,
                mimeType="image/png",
            ),
            TextContent(
                type="text",
                text=f"Скриншот сохранен: {save_path}" if save_path else "Скриншот сделан",
            ),
        ]
    
    elif name == "scroll":
        clicks = arguments["clicks"]
        x = arguments.get("x")
        y = arguments.get("y")
        
        if x is not None and y is not None:
            pyautogui.scroll(clicks, x=x, y=y)
        else:
            pyautogui.scroll(clicks)
        
        direction = "вверх" if clicks > 0 else "вниз"
        return [TextContent(type="text", text=f"Прокрутка {direction} на {abs(clicks)} щелчков")]
    
    elif name == "find_and_click":
        image_path = arguments["image_path"]
        confidence = arguments.get("confidence", 0.9)
        
        try:
            location = pyautogui.locateOnScreen(image_path, confidence=confidence)
            if location:
                center = pyautogui.center(location)
                pyautogui.click(center)
                return [TextContent(type="text", text=f"Элемент найден и кликнут по координатам ({center.x}, {center.y})")]
            else:
                return [TextContent(type="text", text=f"Элемент не найден: {image_path}")]
        except Exception as e:
            return [TextContent(type="text", text=f"Ошибка при поиске элемента: {str(e)}")]
    
    elif name == "wait":
        seconds = arguments["seconds"]
        await asyncio.sleep(seconds)
        return [TextContent(type="text", text=f"Ожидание {seconds} секунд завершено")]
    
    elif name == "get_mouse_position":
        x, y = pyautogui.position()
        return [TextContent(type="text", text=f"Текущая позиция мыши: ({x}, {y})")]
    
    elif name == "get_screen_size":
        width, height = pyautogui.size()
        return [TextContent(type="text", text=f"Размер экрана: {width}x{height}")]
    
    elif name == "run_test_scenario":
        scenario = arguments["scenario"]
        return await run_test_scenario(scenario)
    
    else:
        return [TextContent(type="text", text=f"Неизвестный инструмент: {name}")]


async def run_test_scenario(scenario: str) -> list:
    """Запускает тестовый сценарий для приложения AppClubCalculatoare."""
    
    results = []
    
    if scenario == "login":
        # Сценарий входа в систему
        results.append(TextContent(type="text", text="=== Тестовый сценарий: Вход в систему ==="))
        
        # Делаем скриншот начального экрана
        screenshot = pyautogui.screenshot()
        buffered = io.BytesIO()
        screenshot.save(buffered, format="PNG")
        img_str = base64.b64encode(buffered.getvalue()).decode()
        results.append(ImageContent(type="image", data=img_str, mimeType="image/png"))
        results.append(TextContent(type="text", text="1. Начальный экран сделан"))
        
        # Вводим логин
        pyautogui.typewrite("Alexandru", interval=0.05)
        pyautogui.press("tab")
        pyautogui.typewrite("121212", interval=0.05)
        pyautogui.press("enter")
        
        results.append(TextContent(type="text", text="2. Логин и пароль введены"))
        
        # Ждем загрузки
        await asyncio.sleep(2)
        
        # Делаем скриншот после входа
        screenshot = pyautogui.screenshot()
        buffered = io.BytesIO()
        screenshot.save(buffered, format="PNG")
        img_str = base64.b64encode(buffered.getvalue()).decode()
        results.append(ImageContent(type="image", data=img_str, mimeType="image/png"))
        results.append(TextContent(type="text", text="3. Экран после входа сделан"))
    
    elif scenario == "dashboard":
        # Сценарий проверки дашборда
        results.append(TextContent(type="text", text="=== Тестовый сценарий: Проверка дашборда ==="))
        
        # Делаем скриншот дашборда
        screenshot = pyautogui.screenshot()
        buffered = io.BytesIO()
        screenshot.save(buffered, format="PNG")
        img_str = base64.b64encode(buffered.getvalue()).decode()
        results.append(ImageContent(type="image", data=img_str, mimeType="image/png"))
        results.append(TextContent(type="text", text="Дашборд проверен"))
    
    elif scenario == "crud":
        # Сценарий проверки CRUD-операций
        results.append(TextContent(type="text", text="=== Тестовый сценарий: CRUD-операции ==="))
        
        # Открываем меню оборудования
        pyautogui.hotkey("ctrl", "e")
        await asyncio.sleep(1)
        
        # Делаем скриншот
        screenshot = pyautogui.screenshot()
        buffered = io.BytesIO()
        screenshot.save(buffered, format="PNG")
        img_str = base64.b64encode(buffered.getvalue()).decode()
        results.append(ImageContent(type="image", data=img_str, mimeType="image/png"))
        results.append(TextContent(type="text", text="CRUD-форма открыта"))
    
    elif scenario == "reports":
        # Сценарий проверки отчетов
        results.append(TextContent(type="text", text="=== Тестовый сценарий: Отчеты ==="))
        
        # Открываем меню отчетов
        pyautogui.hotkey("ctrl", "r")
        await asyncio.sleep(1)
        
        # Делаем скриншот
        screenshot = pyautogui.screenshot()
        buffered = io.BytesIO()
        screenshot.save(buffered, format="PNG")
        img_str = base64.b64encode(buffered.getvalue()).decode()
        results.append(ImageContent(type="image", data=img_str, mimeType="image/png"))
        results.append(TextContent(type="text", text="Отчеты открыты"))
    
    else:
        results.append(TextContent(type="text", text=f"Неизвестный сценарий: {scenario}"))
    
    return results


async def main():
    """Запуск MCP-сервера."""
    async with stdio_server() as (read_stream, write_stream):
        await server.run(
            read_stream,
            write_stream,
            server.create_initialization_options(),
        )


if __name__ == "__main__":
    asyncio.run(main())
