\
\ Code to drive a silver smartcard (16f876+24lc64)
\
\ This demonstration program does nothing but send the ATR (Answer To Reset)
\ which identifies the card. It has been written to test the EEPROM read
\ routine.

include libstore.fs
include piceeprom.fs
include picflash.fs

\ ----------------------------------------------------------------------
\ Timer routines
\ ----------------------------------------------------------------------

\ Timer-start initializes the timer

: timer-start ( -- ) 0 tmr0 ! t0if bit-clr ;

\ A bit correspond to 372/fi where fi is the clock frequency. The following
\ word sets the timer to wait until a timer overflow occurs, then to reset
\ the timer so that the next time it overflows exactly one bit has passed.

:: timer-wait ( n -- )
    tmr0 +!
    begin t0if bit-set? until               \ Wait until overflow occurs
    t0if bit-clr                            \ Clear overflow bit
;

: timer-halfbit ( -- ) -$2c timer-wait ;    \ 372/4/2-2=$2c
: timer-1bit ( -- ) -$5b timer-wait ;       \ 372/4-2=$5b
: timer-2bits ( -- ) -$b8 timer-wait ;      \ 372*2/4-2=$b8

: timer-400 ( -- ) -$62 timer-wait ;        \ 400/4-2=$62

\ ----------------------------------------------------------------------
\ Flash as storage
\ ----------------------------------------------------------------------

: flash-set-addr ( addr -- ) eeadr ! ;
: f@c ( addr -- ) eeadr ! flash-read eedata @ ;
: f!c ( byte addr -- ) eeadr ! eedata ! 0 eedath ! flash-write ;

\ ----------------------------------------------------------------------
\ Data and debug
\ ----------------------------------------------------------------------

variable cla variable ins variable p1 variable p2 variable len
create data $20 allot

\ ATR - This one is used only for test purpose, it has been copied from
\ a card I had around when writing this code.

eecreate atr $3f ee, $77 ee, $18 ee, $25 ee, $20 ee,
eecreate send-errors $00 ee,
eecreate framing-errors $00 ee,
eecreate parity-errors $00 ee,
eecreate ready-count $00 ee,
eecreate last-ok-size $00 ee,
eecreate last-cla $00 ee,
eecreate last-ins $00 ee,

:: ee1+! ( addr -- ) dup ee@ 1+ swap ee! ;

: inc-send-errors ( -- ) send-errors ee1+! ;
: inc-framing-errors ( -- ) framing-errors ee1+! ;
: inc-parity-errors ( -- ) parity-errors ee1+! ;
: inc-ready ( -- ) ready-count ee1+! ;
: inc-size ( -- ) last-ok-size ee1+! ;
: record-command ( -- ) cla @ last-cla ee! ins @ last-ins ee! ;

\ ----------------------------------------------------------------------
\ Electrical connections between card and reader
\ ----------------------------------------------------------------------

7 pin-b i/o

: transmission-mode ( -- ) i/o high i/o >output ;
: reception-mode ( -- ) i/o >input ;

\ ----------------------------------------------------------------------
\ Card to reader
\ ----------------------------------------------------------------------

\ Send one or zero bit

variable parity

\ Inverse convention: one is low, zero is high, MSB first

: send-one ( -- ) i/o low timer-1bit ;
: send-zero ( -- ) i/o high timer-1bit ;

\ Send MSB and return shifted byte. LSB contains garbage.

: send-msb ( b -- b' )
    dup parity xor! rlf-tos c bit-set? if send-one exit then send-zero ;

\ Send byte. Parity is xored with each MSB, so the MSB of the parity is the
\ one to send.

variable bitcount

: send-byte ( b -- )
    dup                                            \ Copy (for retransmission)
    timer-start transmission-mode send-one         \ Time base + start bit
    0 parity !
    8 bitcount v-for send-msb v-next drop          \ Send 8 bits
    parity @ send-msb drop                         \ Send parity
    reception-mode timer-1bit                      \ First bit of guard time
    i/o low? if                                    \ Transmission error?
      inc-send-errors timer-2bits recurse exit     \ Yes, resend byte
    then
    drop                                           \ Discard copy
    timer-1bit ;                                   \ Second bit of guard time

:: send-const ( b -- ) send-byte ;

variable tmpee
:: send-eeprom ( addr n -- )
    tmpee v-for dup ee@ send-byte 1+ v-next drop ;

\ ----------------------------------------------------------------------
\ Reader to card
\ ----------------------------------------------------------------------

: wait-for-start-bit ( -- )
    begin i/o high? while repeat             \ Wait for start bit
    timer-start timer-halfbit                \ Move to middle of bit
    i/o high? if
	inc-framing-errors recurse exit      \ Framing error
    then
;

: parity-error ( -- )
    inc-parity-errors timer-halfbit
    transmission-mode i/o low timer-1bit reception-mode
;

: receive-byte ( -- b/w )
    wait-for-start-bit
    0 parity !
    0                                        \ Value to be received on stack
    8 bitcount v-for
      2* timer-1bit i/o low? if 1 + then     \ Set inverted value in LSB
      dup parity xor!
    v-next
    timer-1bit i/o low? if parity invert! then
    timer-halfbit                            \ Consume last half-bit
    parity @ 1 and if drop parity-error recurse exit then
    inc-size >w
;

: receive-header ( -- )
    inc-ready 0 last-ok-size ee!
    receive-byte w> cla ! receive-byte w> ins ! receive-byte w> p1 !
    receive-byte w> p2 ! receive-byte w> len !
    record-command
;

variable datacount
variable dataaddr
: receive-data ( -- )
    data dataaddr !
    len @ datacount v-for
        receive-byte w> dataaddr @ ! 1 dataaddr +!
    v-next
;

\ ----------------------------------------------------------------------
\ Test program
\ ----------------------------------------------------------------------

:: send-const+0 ( b -- ) send-const $00 send-const ;
: success ( -- ) $90 send-const+0 ;
: cla-not-supported ( -- ) $6e send-const+0 ;
: ins-not-supported ( -- ) $6d send-const+0 ;
: send-ack ( -- ) ins @ send-const ;

: handle-getdata ( -- )
    send-ack
    len @ begin $aa send-const 1- while repeat drop success ;

: handle-ca ( -- )
    ins @ $b7 = if success exit then
    ins @ $b8 = if handle-getdata exit then
    ins-not-supported ;

: handle-incoming ( -- )
    timer-2bits                        \ Setup time for the receiver
    cla @ $ca = if handle-ca exit then
    cla-not-supported ;

: send-atr ( -- )
    timer-400                          \ Required delay at the beginning
    atr $c send-eeprom
;

\ Initialization

: init ( -- )
    $08 option_reg !                         \ No prescaler, pull-ups enabled
    reception-mode                           \ Default state is reception mode
;

\ Main program

main : main ( -- )
    init send-atr
    begin receive-header handle-incoming again
;


\ Configuration word

fosc-xt set-fosc      \ Crystal oscillator
false set-wdte        \ Do not use watchdog timer
false set-lvp         \ No low-voltage programming
false set-boden       \ No brown-out reset
false set-wrt         \ Disable flash-writing