// Это скрипт для Google Apps Script
// Вставьте этот код в редактор скриптов вашего Google Sheet (Extensions > Apps Script)
// Затем разверните как веб-приложение (Deploy > New deployment > Web app)
// Скопируйте полученный URL (Webhook URL) в appsettings.json вашего приложения ScanFetch

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

    var sheet = SpreadsheetApp.getActiveSpreadsheet().getActiveSheet();
    var timestamp = new Date(); // Текущая дата и время

    // --- НАСТРОЙКИ ---
    // Диапазон, в котором ищем ключевые слова (например, первая строка, столбцы A-Z)
    var keywordRangeAddr = "A1:Z1"; 
    // Строка, с которой начинаем писать данные, если ключевое слово найдено (будет искать пустую ячейку ниже этой строки)
    var keywordStartRow = 2;
    
    // --- ПАРАМЕТРЫ ДЛЯ "НЕ НАЙДЕНО" ---
    var fallbackColumn = 1; // Столбец A
    var fallbackStartRow = 3; // Начиная с 3-ей ячейки (строки)
    
    // ----------------------

    // Получаем список ключевых слов
    var keywordRange = sheet.getRange(keywordRangeAddr);
    var keywords = keywordRange.getValues()[0]; // Берем первую строку диапазона
    var startColumn = keywordRange.getColumn(); // Индекс начального столбца (1-based)
    
    var foundMatch = false;
    var targetRow = -1;
    var targetCol = -1;

    // 2. Поиск совпадений
    // Приводим код к строке и нижнему регистру для сравнения
    var codeCheck = String(code).toLowerCase();

    for (var i = 0; i < keywords.length; i++) {
      var rawKeyword = keywords[i];
      if (rawKeyword === "" || rawKeyword === null || rawKeyword === undefined) continue;

      var keyword = String(rawKeyword).trim();
      if (!keyword) continue;
      
      var keywordCheck = keyword.toLowerCase();

      // Проверяем: содержит ли полученный код ключевое слово (регистронезависимо)
      if (codeCheck.indexOf(keywordCheck) !== -1) {
        // НАШЛИ СОВПАДЕНИЕ
        targetCol = startColumn + i; // Столбец, соответствующий ключевому слову
        
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
      sheet.getRange(targetRow, targetCol).setValue(code);
      // Справа записываем дату и время
      sheet.getRange(targetRow, targetCol + 1).setValue(timestamp);
    }

    return ContentService.createTextOutput(JSON.stringify({"result": "success", "row": targetRow, "col": targetCol}));

  } catch (error) {
    return ContentService.createTextOutput(JSON.stringify({"result": "error", "message": error.message}));
  } finally {
    lock.releaseLock();
  }
}

// Вспомогательная функция: найти номер первой пустой строки в указанном столбце, 
// начиная с minRow.
function getNextEmptyRow(sheet, column, minRow) {
  var maxRows = sheet.getMaxRows();
  
  // Простая стратегия: смотрим снизу вверх (ctrl+up), чтобы найти последние данные
  // Это работает быстрее, чем перебор.
  
  // Берем ячейку в самом низу столбца
  var lastCell = sheet.getRange(maxRows, column);
  
  // Если сама последняя ячейка не пустая - значит столбец полон, добавляем вниз
  if (!lastCell.isBlank()) {
    return maxRows + 1;
  }
  
  // Прыгаем вверх до ближайших данных
  var lastDataRow = lastCell.getNextDataCell(SpreadsheetApp.Direction.UP).getRow();
  
  // Если мы уперлись в самый верх (строка 1) и она пустая (или это заголовок, который мы не считаем данными при поиске снизу если весь столбец пуст)
  // Корректировка: если колонка визуально пустая, getNextDataCell(UP) вернет 1.
  
  // Давайте проверим, если "последние данные" находятся ВЫШЕ minRow, то возвращаем minRow.
  // Если данные находятся НИЖЕ или РАВНО minRow, то возвращаем data + 1.
  
  // Нюанс: если столбец полностью пуст, lastDataRow будет 1.
  // Если 1-я строка (заголовок) заполнена, lastDataRow будет 1.
  // В обоих случаях, если minRow = 3, target должен быть 3.
  
  var target = lastDataRow + 1;
  if (target < minRow) {
    return minRow;
  }
  return target;
}
