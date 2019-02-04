# Release notes

## 2.0.4
25/06/18 : Fix to add support for reading cards using NFC on LG and HTC devices

## 2.0.3
24/04/18 : Fix to NFC reading + MiM scheme added for v1 QR support

## 2.0.2
24/04/18 : Minor bug fixes

## 2.0.1
03/04/18 :  This has been removed (never distributed)

## 2.0.0
23/03/18 : First release of SDK

# Usage

## Initialise the SDK

        SmartcardClient.init(getApplicationContext(),
                new ISmartcardClientEventHandler<InitTaskResult>() {
                    @Override
                    public void onProgressUpdate(String progressMsg) {
                        addProgress(progressMsg);
                    }
                    @Override
                    public void onPostExecute(SmartcardClientResult<InitTaskResult> result) {
                        if (result.isSuccess()) {
                            addProgress("Initialisation returned success!");
                        } else {
                            // Initialisation failed
                            SmartcardClientError e = result.getError();
                            addProgress(e != null ? e.getFullErrorMessage() : "No error info available!");
                        }
                    }
                },
                new InitTaskParams("my-api-key", "my-app-name", "my-app-version", "SDK Test App"), null);

## Read a card using NFC

        // uses the NFC Intent object returned by the OS...
        SmartcardClient.readCard(getApplicationContext(), new ISmartcardClientEventHandler() {
            @Override
            public void onProgressUpdate(String progressMsg) {
                addProgress(progressMsg);
            }

            @Override
            public void onPostExecute(SmartcardClientResult result) {
                Log.d(TAG, "SDK read card response received.");
                handleReadCardResponse(result, sync);
            }
        }, intent, enumSyncOption.SYNC);

## Read a card using QRCode

        // First read a QR code using the device's camera - set qrCodeData to the QR code data
        CardReadInfo cri = new CardReadInfo(new Date(), "Site hut 22", "London", 0, 0, null);
        ReadCardByQRCodeTaskParams params = new ReadCardByQRCodeTaskParams(qrCodeData, cri);
        SmartcardClient.readCardByQRCode(getApplicationContext(), new ISmartcardClientEventHandler<ReadCardByQRCodeTaskResult>() {
            @Override
            public void onProgressUpdate(String progressMsg) {
                addProgress(progressMsg);
            }

            @Override
            public void onPostExecute(SmartcardClientResult<ReadCardByQRCodeTaskResult> result) {
        if (result.isSuccess()) {
            ReadCardByQRCodeTaskResult qrResult = result.getResult();
            if (qrResult.qrCodeReadResult == enumQRCodeReadResult.SUCCESS) {
                CardData cardData = result.getResult().cardData;
                renderCard(cardData);
            } else if (qrResult.qrCodeReadResult == enumQRCodeReadResult.OFFLINE) {
                // save the qr code so it can be processed when device comes back online
                cachedQRCode = qrResult.qrCodeString;
            } else if (qrResult.qrCodeReadResult == enumQRCodeReadResult.SERVER_REJECTED)
                addProgress("QR code was rejected. Reason: [" + qrResult.serverRejectionReasonMessage + "]." + (qrResult.failureMessage != null ? qrResult.failureMessage : ""));
            else
                addProgress("QR code read FAILED. Reason: [" + qrResult.failureMessage + "].");
        } else {
            String msg = "QR read failed - ";
            SmartcardClientError e = result.getError();
            if (e != null) {
                msg = e.getFullErrorMessage();
            }
        }


progressBar.setVisibility(View.GONE);
            }
        }, params);

