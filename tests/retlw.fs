1 portd bit 5key.pin-MENU
2 portd bit 5key.pin-UP
3 portd bit 5key.pin-DOWN
4 portd bit 5key.pin-OK
5 portd bit 5key.pin-STARTSTOP

0 constant 5key.MENU
1 constant 5key.UP
2 constant 5key.DOWN
3 constant 5key.OK
4 constant 5key.STARTSTOP
5 constant 5key.NONE

: 5key.getc-raw
    5key.pin-MENU      high? if 5key.MENU      >w exit then
    5key.pin-UP        high? if 5key.UP        >w exit then
    5key.pin-DOWN      high? if 5key.DOWN      >w exit then
    5key.pin-OK        high? if 5key.OK        >w exit then
    5key.pin-STARTSTOP high? if 5key.STARTSTOP >w exit then
    5key.NONE >w
; return-in-w

: x 5key.getc-raw ;
