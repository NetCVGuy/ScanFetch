// Это скрипт для Google Apps Script
// Вставьте этот код в редактор скриптов вашего Google Sheet (Extensions > Apps Script)
// Затем разверните как веб-приложение (Deploy > New deployment > Web app)
// Скопируйте полученный URL (Webhook URL) в appsettings.json вашего приложения ScanFetch
//
// ЛОГИКА РАБОТЫ:
// 1. Ключевые слова ищутся в диапазоне F2:Z2 (только четные столбцы: F, H, J, L, N, P, R, T, V, X, Z)
// 2. Если код содержит ключевое слово - запись в столбец с ключевым словом, начиная с строки 3
// 3. Если нет совпадений - запись в столбец A, начиная с строки 3
// 4. Timestamp записывается в соседний столбец справа от кода
//
// ФОРМАТ ЗАПРОСА: POST {"code": "ABC123", "scanner": "Scanner1", "remote": "192.168.1.1:1234"}
// ФОРМАТ ОТВЕТА: {"result": "success", "row": 3, "col": 6} или {"result": "error", "message": "..."}

function doPost(e) {
  var lock = LockService.getScriptLock();
  // Ждем до 30 секунд чтобы избежать состояния гонки при одновременных запросах
  lock.tryLock(30000);

  try {
    // 1. Парсинг данных
    var contents = e.postData.contents;
    var data = JSON.parse(contents);
    var code = data.code;
    
    if (!code) {
      return ContentService.createTextOutput(JSON.stringify({"result": "error", "message": "No code provided"}));
    }

    // ВАЖНО: Явно указываем таблицу и лист
    var spreadsheet = SpreadsheetApp.getActiveSpreadsheet(); // Таблица к которой привязан скрипт
    var sheet = spreadsheet.getSheetByName("TEST"); // Явно указываем лист TEST
    
    if (!sheet) {
      throw new Error("Лист TEST не найден в таблице");
    }
    
    var timestamp = new Date(); // Текущая дата и время

    // --- НАСТРОЙКИ ---
    // Диапазон, в котором ищем ключевые слова (строка 2, столбцы F-Z)
    var keywordRangeAddr = "F2:Z2"; 
    // Строка, с которой начинаем писать данные, если ключевое слово найдено (будет искать пустую ячейку ниже этой строки)
    var keywordStartRow = 3;
    
    // --- ПАРАМЕТРЫ ДЛЯ "НЕ НАЙДЕНО" ---
    var fallbackColumn = 1; // Столбец A
    var fallbackStartRow = 3; // Начиная с 3-ей строки (A3)
    
    // ----------------------

    // Получаем список ключевых слов из диапазона F2:Z2
    var keywordRange = sheet.getRange(keywordRangeAddr);
    var keywords = keywordRange.getValues()[0]; // Берем первую строку диапазона
    var startColumn = keywordRange.getColumn(); // Индекс начального столбца (1-based, F=6)
    
    var foundMatch = false;
    var targetRow = -1;
    var targetCol = -1;

    // 2. Поиск совпадений
    // Приводим код к строке и нижнему регистру для сравнения
    var codeCheck = String(code).toLowerCase();

    for (var i = 0; i < keywords.length; i++) {
      // Проверяем только четные столбцы (F=6, H=8, J=10, ...)
      // F это индекс 0 в массиве keywords, но столбец 6
      // Четные столбцы: 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26 (F, H, J, L, N, P, R, T, V, X, Z)
      var currentColumn = startColumn + i; // Реальный номер столбца
      
      // Пропускаем нечетные столбцы (G=7, I=9, K=11, и т.д.)
      if (currentColumn % 2 !== 0) continue;
      
      var rawKeyword = keywords[i];
      if (rawKeyword === "" || rawKeyword === null || rawKeyword === undefined) continue;

      var keyword = String(rawKeyword).trim();
      if (!keyword) continue;
      
      var keywordCheck = keyword.toLowerCase();

      // Проверяем: содержит ли полученный код ключевое слово (регистронезависимо)
      if (codeCheck.indexOf(keywordCheck) !== -1) {
        // НАШЛИ СОВПАДЕНИЕ
        targetCol = currentColumn; // Столбец, соответствующий ключевому слову
        
        // Ищем первую свободную ячейку в этом столбце, начиная с keywordStartRow
        targetRow = getNextEmptyRow(sheet, targetCol, keywordStartRow);
        
        foundMatch = true;
        break; // Останавливаемся после первого совпадения
      }
    }

    // 3. Обработка "НЕ НАЙДЕНО"
    if (!foundMatch) {
      targetCol = fallbackColumn;
      targetRow = getNextEmptyRow(sheet, fallbackColumn, fallbackStartRow);
    }

    // 4. Запись данных
    if (targetRow > 0 && targetCol > 0) {
      // Записываем код
      var codeCell = sheet.getRange(targetRow, targetCol);
      codeCell.setValue(code);
      
      // Справа записываем дату и время
      var timeCell = sheet.getRange(targetRow, targetCol + 1);
      timeCell.setValue(timestamp);
      
      // Логирование для отладки (будет видно в Executions)
      Logger.log("Записано: код='" + code + "' в ячейку " + codeCell.getA1Notation() + ", время в " + timeCell.getA1Notation());
    } else {
      throw new Error("Некорректные координаты: row=" + targetRow + ", col=" + targetCol);
    }

    return ContentService.createTextOutput(JSON.stringify({
      "result": "success", 
      "row": targetRow, 
      "col": targetCol,
      "code": code,
      "foundMatch": foundMatch,
      "message": code
    }));

  } catch (error) {
    return ContentService.createTextOutput(JSON.stringify({"result": "error", "message": error.message}));
  } finally {
    lock.releaseLock();
  }
}

// Вспомогательная функция: найти номер первой пустой строки в указанном столбце, 
// начиная с minRow.
function getNextEmptyRow(sheet, column, minRow) {
  // Простой и надежный метод: ищем первую пустую ячейку начиная с minRow
  var currentRow = minRow;
  var maxRows = sheet.getMaxRows();
  
  // Ограничиваем поиск разумным количеством строк (1000) для производительности
  var searchLimit = Math.min(maxRows, minRow + 1000);
  
  for (currentRow = minRow; currentRow <= searchLimit; currentRow++) {
    var cell = sheet.getRange(currentRow, column);
    if (cell.isBlank()) {
      return currentRow;
    }
  }
  
  // Если все 1000 строк заполнены, возвращаем следующую за пределами лимита
  return searchLimit + 1;
}

// ПРИМЕР СТРУКТУРЫ ТАБЛИЦЫ:
// 
//     A       B          C    D    E    F         G      H         I      J         K      ...
// 1   -       -          -    -    -    -         -      -         -      -         -
// 2   -       -          -    -    -    Keyword1  -      Keyword2  -      Keyword3  -      (ключевые слова только в четных столбцах F, H, J, ...)
// 3   Code1   Time1      -    -    -    Code2     Time2  Code3     Time3  -         -      (данные начинаются с 3 строки)
// 4   Code4   Time4      -    -    -    Code5     Time5  -         -      Code6     Time6
// 5   Code7   Time7      -    -    -    -         -      Code8     Time8  -         -
//
// Логика:
// - Если код содержит "Keyword1" → запись в столбец F (первая пустая ячейка от F3 и ниже)
// - Если код содержит "Keyword2" → запись в столбец H
// - Если код НЕ содержит ни одного ключевого слова → запись в столбец A (fallback)
// - Timestamp всегда записывается в следующий столбец справа (B для fallback, G для F, I для H, и т.д.)
