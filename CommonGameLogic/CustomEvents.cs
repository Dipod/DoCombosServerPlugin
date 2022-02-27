namespace CommonGameLogic
{
    // this is custom events that sends via photon
    enum CustomEvents
    {
        putCoinFromServer,
        putCoinFromClient,
        addPriceToCoinFromServer,
        addPriceToCoinFromClient,
        fixComboFromServer,
        fixComboFromClient,
        openCoinFromServer,
        openCoinFromClient,
        swapCoinsFromServer,
        swapCoinsFromClient,
        error,
        gameStateSync,
        launchStartGameCountdown,
        startGame,
        clientReadyForGame,
        playerWin,
        playerDisconnected
    }
}