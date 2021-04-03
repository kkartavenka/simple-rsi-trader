## Simple RSi Trader

### Hypothesis

A simple approach for a limit order inter-period trader for a derivative market. The idea is to place a limit order at the start of the period, specifying stop loss and take profit. Once period is over -- close the order whether profit or loss. However if a big change of the price happen -- take a fixed profit.

### Configuration & Optimization

Static configurations are specified in SignalModel class per financial instrument individually. 

Non-constant configurations are specified in ParametersModel class. Some of those parameters will be optimized using Nelder-Mead algorithm. To initialize a random poin