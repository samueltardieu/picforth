\
\ Target specific 16f628 file
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

\ no pir2 pie2
unsupported2 pir2 pie2
unsupported3 osfif cmif eeif      \ pir2
unsupported3 osfie cmie eeie      \ pie2

\ some differences in pir1/pie1
\ EEIE CMIE RCIE TXIE — CCP1IE TMR2IE TMR1IE
unsupported3 sspie adpie pspie
unsupported3 sspif adpif pspif

  7 pie1 bit eeie
  7 pir1 bit eeif
  6 pie1 bit cmie
  6 pir1 bit cmif


\ no ADC
unsupported3 adresh adcon0 ansel 
unsupported2 anresl adcon1

\ osccon osctune unsupported
unsupported2 osccon osctune

\ ssp register unsupported
unsupported2 sspadd sspstat

\ cmcon and vrcon lives elsewhere
unsupported cmcon
$1f constant cmcon
$9f constant vrcon

\ eeprom is at a different place
\ and only 128 bytes vs 256
unsupported2 eecon1 eecon2
unsupported2 eeadr eedata
unsupported2 eedath eeadrh
$9a constant eedata
$9b constant eeadr
$9c constant eecon1
$9d constant eecon2

  7 eecon1 bit eepgd  \ does not exist really
  3 eecon1 bit wrerr
  2 eecon1 bit wren
  1 eecon1 bit wr
  0 eecon1 bit rd

\ Extra oscillator modes

03 constant fosc-extclk
10 constant fosc-intrc-io
11 constant fosc-intrc-clk
12 constant fosc-extrc-io
13 constant fosc-extrc-clk

\ Extra configuration information

1 config-switch-2 set-fcmen
2 config-switch-2 set-ieso
