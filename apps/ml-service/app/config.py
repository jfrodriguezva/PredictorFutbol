from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    api_football_key: str = ""
    odds_api_key: str = ""
    anthropic_api_key: str = ""
    database_url: str = "sqlite:///./data/predictor.db"


settings = Settings()
