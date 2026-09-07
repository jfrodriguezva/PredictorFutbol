from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """
    Runtime configuration for the ML service.

    Values are read from environment variables (or a local .env file, never committed).
    This service never calls API-FOOTBALL directly; it only reads data already
    persisted by the C# backend into SQL Server.
    """

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    environment: str = "Development"
    service_name: str = "SportsPredictor ML Service"
    service_version: str = "0.1.0"

    # Used only by /analyze/football-1x2 for the Claude-generated narrative — falls
    # back to a template when unset (see narrative/generate.py). Never used to call
    # API-FOOTBALL or any other provider directly.
    anthropic_api_key: str = ""


settings = Settings()
