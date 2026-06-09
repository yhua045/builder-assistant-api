#!/bin/bash
cd ./src/Api
export GROQ_KEY=$(dotnet user-secrets list | grep "Groq:ApiKey" | awk -F' = ' '{print $2}')
cd ../../
Groq__ApiKey=$GROQ_KEY docker compose -f docker-compose.dev.yml up