import os.path
import pandas as pd
import os
import inspect
import sys
import sqlite3

CURRENT_DIR = os.path.dirname(os.path.abspath(inspect.getfile(inspect.currentframe())))
parent_dir = os.path.dirname(CURRENT_DIR)
root_dir = os.path.dirname(parent_dir)
New_FILE_NAME =os.path.join(CURRENT_DIR, "HeatLossData.xlsx")
DB_NAME = os.path.join(CURRENT_DIR,"ProjectData.db")

EXCEL_FILE_NAME = r'ClimateData_rev1.xlsx'  # Замените на имя вашего файла
REGION ='Region'
CITY = "City"
# 1. Загрузка Excel-файла в DataFrame

DF= pd.read_excel(EXCEL_FILE_NAME)

class Singleton(type):
    _instances = {}

    def __call__(cls, *args, **kwargs):
        if cls not in cls._instances:
            cls._instances[cls] = super(Singleton, cls).__call__(*args, **kwargs)
        return cls._instances[cls]

class SqlConnector(object):
    __metaclass__ = Singleton
    conn_local_sql: sqlite3.Connection = sqlite3.connect( DB_NAME, check_same_thread=False)

def contains_region_keyword(text):
    """Функция для проверки, содержит ли строка одно из ключевых слов"""
    if isinstance(text, str):  # Убедимся, что это строка
        keywords = ['область', 'регион', 'республика', 'край', 'автономный округ']
        return any(keyword in text.lower() for keyword in keywords)  # Ищем в нижнем регистре
    return False

def Df_to_SQL_with_convert_data():


    # 2. Переименование столбца с регионами (если необходимо)
    region_column_name = CITY

    # 4. Создание столбца "Регион" на основе ключевых слов
    DF[REGION] = DF[region_column_name].apply(lambda x: x if contains_region_keyword(x) else None)

    # 5. Заполнение пропущенных значений (NaN) в столбце "Регион" предыдущим значением (протягивание вниз)
    DF[REGION] = DF[REGION].ffill()

    # 6. Удаление строк, где значение в 'region_column_name' совпадает со значением в 'Регион'
    df = DF[DF[region_column_name] != DF[REGION]]

    # Сохранение в новый Excel-файл (необязательно)
    df.to_excel(New_FILE_NAME, index=False)
    df.to_sql("ClimateData",con=SqlConnector.conn_local_sql,if_exists="replace")
    print(f"File created successfully {New_FILE_NAME}")


def Df_to_SQL_convert_data():
    df = DF[DF[REGION]!=0]
    df.to_sql("ClimateData",con=SqlConnector.conn_local_sql,if_exists="replace")
    print(f"File created successfully ClimateData")
    print(df)

Df_to_SQL_convert_data()
