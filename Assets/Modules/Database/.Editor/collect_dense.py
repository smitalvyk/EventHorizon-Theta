import os
from pathlib import Path

# Расширения файлов, которые мы собираем
TARGET_EXTENSIONS = {'.cs', '.xml', '.json', '.cmd'}
OUTPUT_FILE = 'CombinedCode_Dense.txt'

def minify_and_collect():
    print("Собираю и сжимаю код...")
    
    with open(OUTPUT_FILE, 'w', encoding='utf-8') as out_f:
        # Проходим по всем файлам в текущей папке и подпапках
        for filepath in Path('.').rglob('*'):
            if filepath.is_file() and filepath.suffix.lower() in TARGET_EXTENSIONS:
                try:
                    # Читаем файл (игнорируем ошибки кодировки, если попадется не UTF-8)
                    with open(filepath, 'r', encoding='utf-8', errors='ignore') as in_f:
                        lines = in_f.readlines()
                    
                    # Сжимаем: убираем пустые строки и лишние пробелы (отступы)
                    minified_lines = [line.strip() for line in lines if line.strip()]
                    
                    # Записываем только если файл не пустой
                    if minified_lines:
                        # Минималистичный маркер файла
                        out_f.write(f"|FILE:{filepath.as_posix()}|\n")
                        # Склеиваем строки через \n (чтобы не сломать синтаксис однострочных комментариев)
                        out_f.write('\n'.join(minified_lines) + '\n')
                        
                except Exception as e:
                    print(f"Пропущен: {filepath} ({e})")

    print(f"Готово! Сжатый код сохранен в {OUTPUT_FILE}")

if __name__ == '__main__':
    minify_and_collect()
