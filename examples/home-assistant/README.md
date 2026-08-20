# Home Assistant integration

Home Assistant is optional. MediaFlow can be controlled over REST or MQTT. Both interfaces use the same event-control service, so start/stop behavior is identical.

## 1. Create the MediaFlow configuration once

In the MediaFlow Web UI create:

1. the source Shares for the phones;
2. one shared destination Share;
3. a Source Group containing the phone source Shares.

Copy the Source Group ID and destination Share ID from the REST API (`/api/v1/source-groups/` and `/api/v1/shares`) for the examples below.

## 2. Home Assistant helpers

```yaml
input_boolean:
  vacation_mode:
    name: Vacation mode

input_text:
  vacation_name:
    name: Vacation name
    initial: Vacation 2026
```

# Option A — REST

## REST commands

Replace the URL and the two IDs with your installation values.

```yaml
rest_command:
  mediaflow_vacation_start:
    url: "http://MEDIAFLOW_HOST:8080/api/v1/events/quick-start"
    method: POST
    content_type: "application/json"
    payload: >-
      {
        "name": {{ states('input_text.vacation_name') | to_json }},
        "sourceGroupId": "YOUR_SOURCE_GROUP_ID",
        "destinationShareId": "YOUR_DESTINATION_SHARE_ID",
        "type": "Vacation",
        "destinationFolderTemplate": "{event.name}",
        "operationMode": "SafeMove",
        "conflictStrategy": "AppendSourceName",
        "duplicateStrategy": "SafeMoveToExisting"
      }

  mediaflow_vacation_stop:
    url: "http://MEDIAFLOW_HOST:8080/api/v1/events/quick-stop"
    method: POST
    content_type: "application/json"
    payload: >-
      {
        "name": {{ states('input_text.vacation_name') | to_json }}
      }
```

```yaml
automation:
  - alias: "MediaFlow - start vacation (REST)"
    mode: single
    triggers:
      - trigger: state
        entity_id: input_boolean.vacation_mode
        to: "on"
    actions:
      - action: rest_command.mediaflow_vacation_start

  - alias: "MediaFlow - stop vacation (REST)"
    mode: single
    triggers:
      - trigger: state
        entity_id: input_boolean.vacation_mode
        to: "off"
    actions:
      - action: rest_command.mediaflow_vacation_stop
```

# Option B — MQTT

Enable MQTT in the MediaFlow Docker configuration first:

```yaml
MediaFlow__Mqtt__Enabled: "true"
MediaFlow__Mqtt__Host: "192.168.1.10"
MediaFlow__Mqtt__Port: "1883"
MediaFlow__Mqtt__BaseTopic: "mediaflow"
# MediaFlow__Mqtt__Username: "mediaflow"
# MediaFlow__Mqtt__Password: "..."
```

MediaFlow subscribes to:

```text
mediaflow/events/command
```

and publishes command results to:

```text
mediaflow/events/state
```

A retained service status is published to:

```text
mediaflow/status
```

### Home Assistant MQTT automations

```yaml
automation:
  - alias: "MediaFlow - start vacation (MQTT)"
    mode: single
    triggers:
      - trigger: state
        entity_id: input_boolean.vacation_mode
        to: "on"
    actions:
      - action: mqtt.publish
        data:
          topic: mediaflow/events/command
          payload: >-
            {
              "action": "quick-start",
              "name": {{ states('input_text.vacation_name') | to_json }},
              "sourceGroupId": "YOUR_SOURCE_GROUP_ID",
              "destinationShareId": "YOUR_DESTINATION_SHARE_ID",
              "type": "Vacation",
              "operationMode": "SafeMove",
              "conflictStrategy": "AppendSourceName",
              "duplicateStrategy": "SafeMoveToExisting"
            }

  - alias: "MediaFlow - stop vacation (MQTT)"
    mode: single
    triggers:
      - trigger: state
        entity_id: input_boolean.vacation_mode
        to: "off"
    actions:
      - action: mqtt.publish
        data:
          topic: mediaflow/events/command
          payload: >-
            {
              "action": "quick-stop",
              "name": {{ states('input_text.vacation_name') | to_json }}
            }
```

MQTT also supports commands for an existing event ID:

```json
{"action":"start","eventId":"EVENT_GUID"}
```

```json
{"action":"stop","eventId":"EVENT_GUID"}
```

`quick-start` is idempotent when an event with the same name, Source Group and destination is already active. A closed event is never reopened by `start`, because its historical capture interval must remain unchanged for late synchronization.

## Safety note

MediaFlow ships with Dry Run enabled. In Dry Run mode the background worker discovers, indexes and matches media, but it will not copy, move or delete files. MQTT controls event windows only; it does not bypass the Dry Run / Live transfer safety gate.
