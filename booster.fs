\
\ Code for the DCC booster.
\
\ The DCC booster is in charge of driving a H bridge, which amplifies a
\ DCC signal so that it can power a model railroad circuit.
\
\ This device is in charge of checking for high current (short-circuit)
\ and thermal warning problems. Note that short-circuit may happen if the
\ circuit does have a reverse loop. In this case, if a jumper on the
\ board allows it, the signal phase will be reversed and a few µs later
\ we will recheck for the short-circuit. If it has disappeared, the DCC
\ signal will stay reversed until a new short-circuit occurs.
\

pic16f87x

\ Port mapping

5 pin-a jumper               \ Inversion allowed jumper (in)
0 pin-b thermal              \ H bridge thermal sensor (in)
1 pin-b presence             \ Presence detected (out)
2 pin-b inversion            \ Inversion active (out)
3 pin-b disconnection        \ Disconnection (out)
6 pin-b loco-detected        \ Engine detected on track (in)
7 pin-b short-detected       \ Short-circuit detected (in)
1 pin-c brake                \ H bridge BRAKE (out)
2 pin-c pwm                  \ H bridge PWM (out)

\ Enable and disable output (act and signal)

: enable-output ( -- ) brake low pwm high disconnection low ;

: disable-output ( -- ) brake high pwm low disconnection high ;

\ Timer routines. At 4MhZ, with a prescaler of 64, each tick corresponds
\ to 64µs. Call ticks with a "negate"d value. The minimum watchdog time is
\ 7ms; it needs to be cleared within the loop.

:: ticks ( -n -- )
    tmr0 !                                   \ Store -n into tmr0
    t0if bit-clr                             \ Clear overflow bit
    begin clrwdt t0if bit-set? until         \ Wait for overflow to occur
;

: 64µs ( -- ) -1 ticks ;
: 10ms ( -- ) -$9d ticks ;

variable scount
: 1s ( -- ) $64 scount v-for 10ms v-next ;

\ Handle track shortcut

: handle-shortcut ( -- )
    \ Toggle inversion if inversion is allowed (jumper high)
    jumper high? if inversion toggle then
    \ If shortcut is gone after 64µs, exit
    64µs short-detected low? if exit then
    \ We are still in shortcut condition, disable output for 10ms
    disable-output 10ms enable-output
;

\ Maintain output disabled as long as thermal alert is on. Wait for one
\ extra second before reenabling output.

: handle-thermal-alert ( -- )
    disable-output
    begin clrwdt thermal low? until
    1s enable-output
;

\ If there is a loco, repeat the signal

: check-loco ( -- )
    loco-detected high? if presence high then
    loco-detected low? if presence low then
;

\ Initialization

: init ( -- )
    $00 portb !                              \ Clear portb output latches
    $00 portc !                              \ Clear portc output latches
    $06 adcon1 !                             \ Disable A/C converter
    $ff trisa !                              \ All pins of porta as inputs
    $f1 trisb !                              \ Portb 1, 2 and 3 as outputs
    $f9 trisc !                              \ Portc 1 and 2 as outputs
    $05 option_reg !                         \ CLKOUT, prescaler(tmr0)=64
    enable-output
;

\ Main program

: mainloop ( -- )
  begin
    short-detected high? if handle-shortcut then
    thermal high? if handle-thermal-alert then
    check-loco
  again
;

main
: main ( -- ) init mainloop ;

\ Configuration word

fosc-hs set-fosc    \ High-speed oscillator
