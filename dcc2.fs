\ dcc2.fs $Id: dcc2.fs 1.1.1.8 Mon, 28 Oct 2002 13:40:58 +0100 sam $

pic16f87x

include libfetch.fs
include libstore.fs

\ Configuration

fosc-hs set-fosc
1 set-wdte
no-cp set-cp
0 set-wrt      \ to prevent mistakes

\ The reset-flag variable, if set, will cause a reset to be sent and the
\ various engines to stop immediately

variable reset-flag

\ Engines

variable next-engine  \ next engine to run

create engine-speeds 8 allot

: engine-addr ( n -- ) engine-speeds + ;

: speed ( n -- ) engine-addr c@ ;

\ set-speed sets engine speed and sets this engine as the next one to be
\ transmitted, to minimalize command latency
: set-speed ( s n -- ) dup next-engine ! engine-addr c! ;

\ set-timer will wait for 3.2*(256-n)-0.2 탎 for a 20MhZ oscillator and a
\ 16 prescaler. For ones, use n=238 (57.4탎 => 1.03% error). For zero, use
\ values between n=0 (819.0탎) and n=224 (102.2탎). If necessary, the range
\ will be adjusted by testing the timer a different
: set-timer ( n -- )
    tmr0 +! ;

variable zero

\ handle-incoming handles incoming data
\ At this time, format is trivial: 00000xxx is for engine number,
\ 01xxxxxx is for speed. Engine number has to be sent before speed.
\ 1xxxxxxx is a reset.

variable incoming-engine

: handle-incoming-engine ( c -- )
    incoming-engine ! ;

: handle-incoming-speed ( c -- )
    incoming-engine @ set-speed ;

: handle-reset ( -- )
    1 reset-flag ! ;

: handle-incoming ( c -- )
    dup 80 and if drop handle-reset exit then
    dup 40 and if handle-incoming-speed exit then
    handle-incoming-engine ;

\ handle-serial handles incoming data and terminates with a call to
\ wait-for-completion (see idle-loop for explanation)

: handle-serial ( -- )
    \ read and handle byte
    rcreg c@ handle-incoming
    \ fallthrough wait-for-completion
    
\ wait-for-completion waits for the timer to overflow with minimal latency
\ and clears the interrupt.

code wait-for-completion
    intcon t0if btfss
    wait-for-completion goto
    intcon t0if bcf
    return
end-code

\ idle-loop does whatever is needed between pulses. It has to terminate
\ with a call to wait-for-completion to wait until next pulse time. We do
\ the same thing with handle-serial, so that we spare one more return stack
\ level.

code idle-loop ( -- )
    rcsta rcif btfsc           \ if a byte has been received
    handle-serial goto         \ then handle it
    wait-for-completion goto   \ or wait
end-code
    
\ Pulses handling is done on port B, on ports 1 and 3, inversion on 0 and 2
\ and enable on 4 and 5

variable portb-admin       \ Inversion on bits 0 and 2, enable on 4 and 5
variable portb-signal      \ Bits 1 and 3
variable portb-invert-mode \ Bits 0 and 2 are on if inversion is enabled

\ Enabling/disabling and inversion of DCC signals

: enable-dcc1 ( -- ) 10 portb-admin or! ;
: disable-dcc1 ( -- ) 10 portb-admin /and! ;
: enable-dcc2 ( -- ) 20 portb-admin or! ;
: disable-dcc2 ( -- ) 20 portb-admin /and! ;

: invert-dcc1 ( -- ) 1 portb-admin xor! ;
: invert-dcc2 ( -- ) 4 portb-admin xor! ;

\ set-portb must be called with 0 for low pulse and a for high-pulse
\ (corresponding to the appropriate portb values in non-inverted mode)

code set-portb ( -- )
    portb-admin ,w rlf          \ Move inversion on port 1 and 3
    portb-invert-mode ,w andwf  \ Limit inversion to loop tracks
    portb-signal ,w xorwf       \ Apply inversion
    a andlw                     \ Limit to signals
    portb-admin ,w iorwf        \ Add inversion and enable outputs
    portb movwf                 \ Apply outputs
    return
end-code

\ check-short checks whether a short-circuit has been detected on tracks.
\ In this case, signal will be reversed or shut down, depending on the
\ shortcut time.

variable track1-short
variable track2-short

: handle-short-track1 ( -- )
    \ If track was already in short circuit, disable it
    track1-short @ if disable-dcc1 exit then
    \ Otherwise, invert it
    invert-dcc1
    1 track1-short +!
;

code check-short-track1 ( -- )
    portb 6 btfsc               \ If track1 is in short circuit
    handle-short-track1 goto    \ then handle it
    track1-short clrf           \ or clear the indicator
    return
end-code

: handle-short-track2 ( -- )
    \ If track was already in short circuit, disable it
    track2-short @ if disable-dcc2 exit then
    \ Otherwise, invert it
    invert-dcc2
    1 track2-short +!
;

code check-short-track2 ( -- )
    portb 7 btfsc               \ If track2 is in short circuit
    handle-short-track2 goto    \ then handle it
    track2-short clrf           \ or clear the indicator
    return
end-code

: check-short ( -- ) check-short-track1 check-short-track2 ;

\ pulse work the reverse way around: it sets the timer to the proper
\ value, then call the idle-loop to do other tasks, then wait for the
\ timer to expire and immediately change the DCC output state.  This
\ way, it is permitted to take some time (up to 40탎, i.e. around 200
\ instructions) to handle secondary tasks. More complex tasks can be
\ handled if the zero flag is true (in which case we have up to 80탎,
\ i.e. around 400 instructions)

: half-pulse ( n -- )
    ]asm clrwdt asm[           \ Clear watchdog counter at least every 18ms
    set-timer idle-loop ;

: pulse ( low high -- )
    half-pulse 0 portb-signal ! check-short set-portb
    half-pulse a portb-signal ! check-short set-portb
;

: stop-transmit ( -- ) \ alias for stop-bit
: stop-bit ( -- ) \ alias for bit-one
: bit-one ( -- )
    0 zero ! ee dup pulse ;

variable checksum

: start-bit ( -- ) \ alias to bit-zero
: bit-zero ( -- ) \ no zero stretching at this time
    1 zero ! 64 dup pulse ;

variable bit-counter

: start-transmit ( -- ) \ alias for preamble
: preamble ( -- )
    10 bit-counter v-for bit-one v-next
    0 checksum ! ;

variable transmit-byte

code send-msb-rlf
    transmit-byte ,f rlf
    status c btfsc
    bit-one goto
    bit-zero goto
end-code

: send-byte ( b -- )
    dup transmit-byte ! checksum xor!
    send-msb-rlf send-msb-rlf send-msb-rlf send-msb-rlf
    send-msb-rlf send-msb-rlf send-msb-rlf send-msb-rlf
;

: send-checksum ( -- ) checksum @ send-byte ;

: send-byte-0 ( -- ) 0 send-byte ;

: send-byte-ff ( -- ) ff send-byte ;

: idle-packet ( -- )
    start-transmit
    send-byte-ff send-byte-0 send-byte-ff
    stop-transmit ;

: reset-packet ( -- )
    start-transmit
    send-byte-0 send-byte-0 send-byte-0
    stop-transmit ;

: send-engine-speed ( n -- )
    start-transmit
    dup send-byte speed send-byte send-checksum
    stop-transmit ;

\ Misc led handling

: led1-on ( -- ) 2 portc or! ;
: led1-off ( -- ) 2 portc /and! ;
: led1-toggle ( -- ) 2 portc xor! ;

: led2-on ( -- ) 4 portc or! ;
: led2-off ( -- ) 4 portc /and! ;
: led2-toggle ( -- ) 4 portc xor! ;

: init-porta ( -- )
    0 porta !   \ Clear output latches
    6 adcon1 !  \ Disable A/C converter
    f0 trisa !  \ Ports 0 to 3 as outputs, 4 to 7 as inputs
;

: init-portb ( -- )
    0 portb !   \ Clear output latches
    c0 trisb !  \ Ports 0 to 5 as outputs, 6 and 7 as inputs
    \ Interrupts should be configured here
;

: init-portc ( -- )
    0 portc !             \ Clear output latches
    ]asm t1con t1oscen bcf asm[  \ Disable timer 1 oscillator
    ]asm t1con tmr1cs bcf asm[   \ Internal timer 1 clock source
    99 trisc !
    \ Initialize i2c and serial here
;

: init-ports ( -- )
    init-porta init-portb init-portc
;

: init-timer ( -- )
    \ CLKOUT, prescaler to timer 0, prescaler=16
    3 option_reg !
    ;

: increment-engine ( -- )
    1 next-engine +! 7 next-engine and! ;

variable engine-counter

: reset-engines ( -- )
    reset-packet reset-packet reset-packet reset-packet
    8 engine-counter v-for
      60 engine-counter @ set-speed \ Set speed 01100000b (full stop)
    v-next
    0 next-engine !
;

: check-reset ( -- )
    reset-flag @ if 0 reset-flag ! reset-engines exit then
;

: mainloop ( -- )
    begin
	check-reset
	next-engine @ increment-engine send-engine-speed
    again
;

: init-serial ( -- )
    \ asynchronous no-tx high-speed
    4 txsta !
    \ serial-enable 8bits-rx continuous-receive
    90 rcsta !
    \ 9600 bauds with BGRH high at 20MhZ
    81 spbrg !
    ;

: init-dcc ( -- )
    30 portb-admin !      \ Both DCC ports enabled, no inversion
    4 portb-invert-mode ! \ Inversion allowed on second DCC port
    0 track1-short !      \ Tracks are not in short circuit
    0 track2-short !
    1 reset-flag !        \ Force a reset at startup
;

: init-leds ( -- ) led1-on led2-on ;

: init ( -- ) init-ports init-serial init-timer init-dcc init-leds ;

main
: main
    init mainloop
;

