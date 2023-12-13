#!/bin/bash
if [[ -z "${TEMPORAL_ADDRESS}" ]]; then
  echo "Warning: TEMPORAL_ADDRESS is undefined. Using local server"
fi 

if [[ -z "${TEMPORAL_NAMESPACE}" ]]; then
  echo "Warning: TEMPORAL_NAMESPACE is undefined."
fi

if [[ -z "${TEMPORAL_CERT_PATH}" ]]; then
  echo "Warning: TEMPORAL_CERT_PATH is undefined."
fi

if [[ -z "${TEMPORAL_KEY_PATH}" ]]; then
  echo "Warning: TEMPORAL_KEY_PATH is undefined."
fi

dotnet run execute-workflow