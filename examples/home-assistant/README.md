# Home Assistant integration example

Home Assistant is optional. MediaFlow events can later be controlled over REST or MQTT.

Suggested entities:

```yaml
input_boolean:
  vacation_mode:
    name: Vacation mode

input_text:
  vacation_name:
    name: Vacation name
```

The implementation-specific MQTT/REST automation will be added once the API is implemented.
