
\ libroll.fs
\ 
\ Implements 'roll' and '-roll' words for PicForth
\ 
\ Contributed by David McNab <david@rebirthing.co.nz>, copyright (c) 2004
\ Released under the GNU Library General Public License (LGPL),
\ available from http://www.gnu.org
\ 
\ This implementation uses tmp1 to store count, and tmp2 to store temporary
\ stack cell, and creates an additional temp variable tmp3 for restoring
\ fsr in -roll

udata
variable tmp3
idata

host

\ Words to manipulate temporary variables

: w>tmp3 [ tmp3 ] literal movwf ;
: tmp3>w [ tmp3 ] literal ,w movf ;

target

\ roll - compliant with gforth

: roll  ( x1 x2 ... xn n -- x2 ... xn x1 )

    dup 0= if drop exit then
    suspend-interrupts
  ]asm
            >w
            w>tmp1    \ save count
    fsr ,f  addwf
    indf ,w movf
            w>tmp2    \ save nth stack cell
label: r-loop       \ n times, copy cell i to position i+1
    fsr ,f  decf
    indf ,w movf
    fsr ,f  incf
    indf    movwf
    fsr ,f  decf
    tmp1 ,f decf
    z       btfss
    r-loop  goto

    \ finish up - put stored nth item to tos
            tmp2>w
    indf    movwf
  asm[
    restore-interrupts
;


\ -roll - non-standard - does the opposite of roll

: -roll  ( x1 x2 .. xn n -- xn x1 x2 .. xn-1 )

    dup 0= if drop exit then
    suspend-interrupts
  ]asm
            >w
            w>tmp1      \ save count
            w>tmp3      \ and another copy so we can restore fsr
    indf ,w movf
            w>tmp2      \ save tos
label: -r-loop        \ n times, copy celli+1 to celli
    fsr ,f  incf
    indf ,w movf
    fsr ,f  decf
    indf    movwf
    fsr ,f  incf
    tmp1 ,f decf
    z       btfss
    -r-loop goto
    
    \ finish up - put stored 0th item to cell i
            tmp2>w
    indf    movwf
            tmp3>w
    fsr ,f  subwf
  asm[
    restore-interrupts
;

