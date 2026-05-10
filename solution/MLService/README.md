# MLService

FastAPI сервис аналитики событий.

## Запуск

1. Создать виртуальное окружение:
   `python3 -m venv .venv`
2. Установить зависимости:
   `source .venv/bin/activate`
   `pip install -r requirements.txt`
3. Запустить:
   `uvicorn app:app --host 0.0.0.0 --port 8000 --reload`

## API

- `POST /cluster`
- `POST /classify`
- `POST /train`
- `POST /register-event`
- `GET /health`
