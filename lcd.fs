\ Use a different part of the FLASH

$1800 org

include picflash.fs
include libstrings.fs

0 pin-a led-green
1 pin-a led-red

\ Basic timing routines at 20MHz

variable timing1 variable timing2
: 80탎 $64 timing1 v-for clrwdt v-next ;
\ : 80탎 $86 timing1 v-for v-next ;
: 1.6ms $a timing1 v-for $c8 timing2 v-for clrwdt v-next v-next ;
\ : 1.6ms $e timing1 v-for $c8 timing2 v-for clrwdt v-next v-next ;
: 30ms $bc timing1 v-for $c8 timing2 v-for clrwdt v-next v-next ;
\ : 30ms $c8 timing1 v-for $fa timing2 v-for v-next v-next ;

: 6s $c8 tmp1 v-for 30ms v-next ;

\ Four bits LCD handling

5 pin-a lcd-d4       \ LCD data (in out)
2 pin-a lcd-d5       \ LCD data (in out)
4 pin-a lcd-d6       \ LCD data (in out)
3 pin-a lcd-d7       \ LCD data (in out)

0 pin-c lcd-rw       \ LCD R/W (out)
1 pin-c lcd-enable   \ LCD enable (out)
3 pin-c lcd-rs       \ LCD register select (out)

: lcd-ports-init
  $06 adcon1 ! $06 sspcon !
  $f4 trisc !
  lcd-enable low lcd-rs low lcd-rw low
  lcd-d4 >output lcd-d5 >output lcd-d6 >output lcd-d7 >output ;

: lcd-pulse lcd-enable high nop nop nop lcd-enable low ;

\ Because of what seems to be a bug in the PIC 16f876, port A4 must be
\ written last as its content will always return 0 despite it being an
\ output and not being tied to timer 0.

: lcd-send4lsb
  lcd-d7 low dup 8 and if lcd-d7 high then
  lcd-d5 low dup 2 and if lcd-d5 high then
  lcd-d4 low dup 1 and if lcd-d4 high then
  lcd-d6 low dup 4 and if lcd-d6 high then
  drop lcd-pulse
;

: lcd-send8 dup 4 rshift lcd-send4lsb nop nop lcd-send4lsb 80탎 ;
: lcd-emit lcd-rs high lcd-send8 lcd-rs low ;
: lcd-print begin str-char dup while lcd-emit repeat ;

: lcd-clear $01 lcd-send8 1.6ms ;

: lcd-setup
  lcd-ports-init 30ms
  $3 lcd-send4lsb 30ms
  $3 lcd-send4lsb 80탎
  $3 lcd-send4lsb 80탎
  $2 lcd-send4lsb 80탎
  $28 lcd-send8 $08 lcd-send8 lcd-clear $06 lcd-send8 $0f lcd-send8 ;

: lcd-test c" Bisou ma Doub" lcd-print ;

: led-setup led-red >output led-green >output led-green high led-red toggle ;

main : main led-setup lcd-setup lcd-test reset ;
