\
\ Target specific 16f88 file
\

\ Remove configword and config-mask aliases

unsupported2 configword config-mask

\ Remove unsupported ports

unsupported2 portc pin-c
unsupported2 portd pin-d
unsupported2 porte pin-e

\ Change no-cp constant

meta
: set-cp 2000 config-mask-1 ;
target

2000 constant no-cp 

\ CCP2 is unsupported

unsupported3 ccpr2l ccpr2h ccp2con

\ Extra oscillator modes

03 constant fosc-extclk
10 constant fosc-intrc-io
11 constant fosc-intrc-clk
12 constant fosc-extrc-io
13 constant fosc-extrc-clk

\ Extra configuration information

1 config-switch-2 set-fcmen
2 config-switch-2 set-ieso
