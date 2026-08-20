# Home Assistant integration

Home Assistant is optional. MediaFlow exposes REST endpoints that make a simple vacation-mode toggle possible without requiring Home Assistant to process any files.

## 1. Create the MediaFlow configuration once

In the MediaFlow Web UI create:

1. the source Shares for the phones;
2. one shared destination Share;
3. a Source Group containing the phone source Shares.

Copy the Source Group ID and destination Share ID from the REST API (`/api/v1/source-groups/` and `/api/v1/shares`) for the Home Assistant configuration below.

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

## 3. REST commands

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

## 4. Automations

```yaml
automation:
  - alias: "MediaFlow - start vacation"
    mode: single
    triggers:
      - trigger: state
        entity_id: input_boolean.vacation_mode
        to: "on"
    actions:
      - action: rest_command.mediaflow_vacation_start

  - alias: "MediaFlow - stop vacation"
    mode: single
    triggers:
      - trigger: state
        entity_id: input_boolean.vacation_mode
        to: "off"
    actions:
      - action: rest_command.mediaflow_vacation_stop
```

When the toggle is switched on, MediaFlow creates an active event starting at the current time. Switching it off closes the same named event while preserving its exact historical capture window. Files that only synchronize after the vacation has ended can therefore still be assigned to that event from their EXIF/video capture timestamp.

## Safety note

MediaFlow ships with `MediaFlow:DryRun=true`. In Dry Run mode the background worker discovers, indexes and matches media, but it will not copy, move or delete files. Enable live mode only after checking Routing Preview with representative media.
