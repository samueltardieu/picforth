\
\ Code for the DCC generator.
\
\ This program is in charge of:
\    * generating a valid DCC signal from commands given on the serial line
\    * do some reporting on the serial line
\
\ References:
\    * NMRA DCC Standards and Recommended Practices:
\      http://www.tttrains.com/nmradcc/

include picisr.fs
include piceeprom.fs
include multitasker.fs
include libnibble.fs
include libfetch.fs
include libstore.fs

\ ----------------------------------------------------------------------
\ Pinout used in this program
\ ----------------------------------------------------------------------

0 pin-b dcc-output
1 pin-b led-empty
2 pin-b led-error

\ ----------------------------------------------------------------------
\ Serial port handling
\ ----------------------------------------------------------------------

\ When a task wants to send something, it has to:
\   - wait for tr-length to be zero
\   - write address (in EEPROM) in tr-eeaddr
\   - write length in tr-length
\
\ Note that no lock is necessary as cooperative multitasking ensures that
\ the whole operation is atomic.

variable tr-eeaddr
variable tr-length

task : serial-transmit ( -- )
    begin
	\ Wait until something is ready to be transmitted
	begin tr-length @ 0= while yield repeat
        \ Get character on stack and mark it as consumed
        tr-eeaddr @ ee@
        1 tr-eeaddr +! 1 tr-length -!
	\ Wait for authorization to transmit
	begin txif bit-clr? while yield repeat
	\ Transmit data
	txreg !
    again
;

: clear-errors ( -- ) oerr bit-set? if cren bit-clr cren bit-set then ;

macro
: key ( -- c ) begin rcif bit-clr? while yield repeat rcreg @ clear-errors ;
target

variable cmd-length
create cmd 7 allot
variable nibble

task : serial-receive ( -- )
    begin
label: cmd-wait
	\ Send prompt when ready to accept a command
        begin cmd-length @ while yield repeat
	begin tr-length @ while yield repeat
	l" Enter a command between `!' and `.'" tr-length ! tr-eeaddr !
	\ Wait for '!' which indicates a command start
	key [char] ! <> if ]asm cmd-wait goto asm[ then
label: cmd-start
	\ Get the command (up to 7 hex bytes, followed by ".")
	0 nibble ! cmd
	begin
	    \ A '!' indicates a framing problem, goto start
	    key dup [char] ! = if 2drop ]asm cmd-start goto asm[ then
            \ A 'r' indicates a reset
	    dup [char] r = if reset then
	    dup [char] . <> while
	    hex>nibble
	    nibble @ if
		\ Shift high nibble and add low nibble
		nibble @ 4 lshift or
		\ Store in memory
                over !
		\ Reset nibble and increment address
		0 nibble ! 1+
	    else
		\ In case nibble is zero, the highest bit is set
		80 or nibble !
	    then
	repeat drop cmd - cmd-length !     \ Command starts
    again
;

\ ----------------------------------------------------------------------
\ Interrupt driven DCC pulse generator
\ ----------------------------------------------------------------------

\ set-timer will wait for 3.2*(256-n)-0.2 탎 for a 20MHz oscillator and a
\ 16 prescaler. For ones, use n=238 (57.4탎 => 1.03% error). For zero, use
\ values between n=0 (819.0탎) and n=224 (102.2탎).

macro
: set-timer ( n -- ) tmr0 +! ;
target

\ delay contains the duration of the next low pulse when the high pulse
\ is in progress or 0 otherwise
\ Warning: those words (pulse, bit-zero and bit-one) terminate with a call
\ to isr-exit, thus returning from interrupt

variable delay

\ pulse and half-pulse work the reverse way around: they set the timer
\ to the proper value, then call the idle-loop to do other tasks, then
\ wait for the timer to expire and immediately change the DCC output
\ state.  This way, it is permitted to take some time (up to 40탎,
\ i.e. around 200 instructions) to handle secondary tasks. More
\ complex tasks can be handled if the zero flag is true (in which case
\ we have up to 80탎, i.e. around 400 instructions).

macro
: half-pulse ( -- ) delay @ set-timer ;          \ Set timer to pulse value
target

\ isr-exit is a word doing isr-restore-return, to avoid repeating the
\ code multiple times

code isr-exit ( -- )
    isr-restore-return
end-code

:: pulse ( duration -- ) delay ! half-pulse isr-exit ;

: bit-zero ( -- ) $64 pulse ;
: bit-one ( -- )  $ee pulse ;

\ Data being transmitted. We implement a 16 bits shift register, left-aligned,
\ so that preamble, start and stop bits can be easily implemented. To write
\ something into the DCC generator, wait for data-length to be zero, write
\ into data-high and data-low (left aligned) and set data-length to the correct
\ number of bits.

variable data-high
variable data-low
variable data-length

\ isr-tmr0 is called when there is a timer overflow interrupt
\ This word is very long to avoid return stack depth exhaustion, but very
\ thoroughly commented. It terminates with a call to isr-exit to properly
\ exit from interrupt

isr : isr-tmr0 ( -- )
    t0if bit-clr                        \ Acknowledge interrupt
    delay @ if
	dcc-output low                  \ Drive DCC output low
	half-pulse                      \ Reset timer and clear watchdog
	0 delay !                       \ Note low pulse in progress
	isr-exit exit                   \ Properly terminate isr
    then
    dcc-output high                     \ Drive DCC output high
    1 data-length -!                    \ Decrement data length
    c bit-set? if                       \ If there is nothing to transmit
      led-error high                    \ signal error
      0 data-length !                   \ and reset length
      ( Note that we cannot reset data, as a write sequence may be in progress )
    then
    led-error low
    data-low rlf! data-high rlf!        \ Shift data left through carry
    c bit-set? if bit-one exit then     \ Transmit one
    bit-zero                            \ Transmit zero
;

\ ----------------------------------------------------------------------
\ DCC data manipulation
\ ----------------------------------------------------------------------

variable checksum

\ Wait for the transmission slot to be available

macro
: wait-for-transmission ( -- ) begin data-length @ while yield repeat ;
target

\ Transmit a preamble (14 bits to 1) and a start bit (one bit to 0)

: preamble ( -- )
    0 checksum !
    $ff data-high ! $fc data-low ! $f data-length ! ;

\ send-byte sends a byte followed by a start bit. send-final-byte ends
\ the packet with a stop bit after the byte. send-checksum sends the checksum
\ with a stop bit.

: finish-byte ( b -- ) dup checksum xor! data-high ! 9 data-length ! ;

: send-byte ( b -- ) $00 data-low ! finish-byte ;

: send-final-byte ( b -- ) $80 data-low or! finish-byte ;

: send-checksum ( -- ) checksum @ send-final-byte ;

\ ----------------------------------------------------------------------
\ DCC command feeder
\ ----------------------------------------------------------------------

\ High level command feeder. This task takes care of feeding complete
\ commands to the DCC engine. If a command is ready (cmd-length is not
\ zero), then the command will be sent to the DCC generator and
\ cmd-length sent to zero at the end. Otherwise, an idle packet will
\ be generated.

variable current-food

task : feeder ( -- )
    begin
        wait-for-transmission preamble
        cmd-length @ if              \ If a command is ready, send it
            led-empty low cmd current-food !
            begin
                current-food @ @ wait-for-transmission send-byte
                1 cmd-length -! z bit-clr? while
                1 current-food +!
            repeat
        else                         \ Otherwise send an idle packet
            led-empty high
            wait-for-transmission $ff send-byte
            wait-for-transmission 0 send-byte
        then
        wait-for-transmission send-checksum
    again
;

\ ----------------------------------------------------------------------
\ Initialization
\ ----------------------------------------------------------------------

: init ( -- )
    \ Data
    $ff data-high !              \ 16 bits to one will help the system
    $ff data-low !               \ to enter permanent mode
    $10 data-length !
    $00 cmd !                    \ Start with a general reset
    $00 cmd 1+ !                 \ (double-zero packet)
    2 cmd-length !
    \ Port B
    $00 portb !                  \ Clear output latches
    $f8 trisb !                  \ Ports 0, 1 and 2 as output
    \ Serial port transmit (Microchip DS30292C page 100)
    portc 6 >output              \ Port C6 is TX
    \ $14 spbrg !                  \ 57600 bauds with BGRH high at 20MhZ
    $19 spbrg !                  \ 9600 bauds with BGRH high at 4MhZ
    $24 txsta !                  \ Asynchronous transmit high-speed generator
    \ Serial port receive (Microchip DS30292C page 102)
    $90 rcsta !                  \ Serial-enable 8bits-rx continuous-receive
    \ Welcome message
    l" Welcome to the DCC generator" tr-length ! tr-eeaddr !
    \ Timer
    $03 option_reg !             \ CLKOUT, prescaler to timer 0, prescaler=16
    t0ie bit-set                 \ Enable timer 0 overflow interrupt
    $f0 tmr0 !                   \ Do not wait too long before first interrupt
    enable-interrupts
;

\ ----------------------------------------------------------------------
\ Main program and main loop
\ ----------------------------------------------------------------------

main
: main ( -- ) init multitasker ;

\ ----------------------------------------------------------------------
\ Configuration word
\ ----------------------------------------------------------------------

fosc-hs set-fosc    \ High-speed oscillator
false set-/pwrte    \ Power-up reset timer
false set-lvp       \ No need for low voltage programming
