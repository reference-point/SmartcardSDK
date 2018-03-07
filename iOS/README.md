# Smartcard SDK for iOS

## Requirements
* iOS 9.0+
* Swift 4+
* Xcode 9+

## Integration
### Manually
1. download and unzip the release file SmartcardSDK.ios.zip
2. select your project target, add SmartcardSDK.framework as an embedded binary
3. add dependency library [SwiftyJSON](https://github.com/SwiftyJSON/SwiftyJSON) by adding SwiftyJSON.swift to the project tree

## Usage
```swift
import SmartcardSDK

let initParams = InitParams(apiKey: apiKey,
                            appIdentifierName: appIdentifierName,
                            appVersion: appVersion,
                            appFriendlyName: appFriendlyName)
SmartcardClient.shared.initClient(initParams: initParams) {
    result in
    ...
}
```
