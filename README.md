# NZ Covid Pass Linker Bot

This is a telegram bot made to store and verify a user' self submitted COVID vaccination status. It uses the COVID pass as the main proof of vaccination.

COVID passes when created are stored in the Ministry of Health's systems. To verify it, the MoH issues a digital signature that takes the form of a QR code. If a QR code is submitted to this bot, it is verified via the MoH system and if valid, will mark the submitting Telegram account as valid (with an expiry date). Only one Telegram account can have one unique pass at any given time.

No sensitive information will be directly stored by this service at the current time, hashes will be used in all places where it can. In the future (if there is the need), the bot may explicitly ask for further data that may identify you. During verification, your pass will transit through this server, but the data is not kept.

This service and website is running under the ownership of [@TheGuardianWolf](https://t.me/theguardianwolf).