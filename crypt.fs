\ Crypt using only 8 bits words

include libfetch.fs
include libstore.fs
include piceeprom.fs

\ Algorithm data

\ XXXXX create input 8 allot
create input 01 , 23 , 45 , 67 , 89 , AB , CD , EF ,
create permuted 8 allot
\ XXXXX create key 7 allot
create key 13 , 34 , 57 , 79 , 9B , BC , DF , F1 ,
create expanded 6 allot
variable pass
variable src
variable dst
variable mask
variable bit-count

: src@ ( -- b ) src @ @ ;
: src--@ ( -- b ) 1 src -! src@ ;
: src@++ ( -- b ) src@ 1 src +! ;
: dst! ( b -- ) dst @ ! ;
: dst!++ ( b -- ) dst! 1 dst +! ;

\ Initial permutation

: init-permutation ( -- ) input src ! permuted dst ! ;
: one-bit ( b -- b' ) 2* src--@ mask @ and if 1 or then ;
: one-byte ( -- ) 8 src +! 0 8 bit-count v-for one-bit v-next dst!++ ;
: next-byte ( --  ) c bit-clr mask rrf! mask rrf! one-byte ;
: make-permutation ( -- )
    one-byte next-byte next-byte next-byte
    next-byte next-byte next-byte next-byte
;
: ip ( -- ) init-permutation 40 mask ! make-permutation ;

\ Key shifting

code key-shift1 ( -- )
    c bcf
    key 3 + 3 btfss
    c bsf
    key 6 + ,f rlf
    key 5 + ,f rlf
    key 4 + ,f rlf
    key 3 + ,f rlf
    key 2 + ,f rlf
    key 1 + ,f rlf
    key ,f rlf
    key 3 + 4 bcf
    c btfsc
    key 3 + 4 bsf
    return
end-code

: 2nd-shift ( -- ) pass @ 2 >= if pass @ 7 and 7 <> if key-shift1 then then ;
: key-shift ( -- ) key-shift1 2nd-shift ;

decimal

\ Substitution tables coming from the DES standard have been reordered so
\ that the substitution quartet for the 6 bits value N is directly the Nth
\ element of the table.

eetable subst1
  table> 14 0 4 15 13 7 1 4 2 14 15 2 11 13 8 1 
  table> 3 12 10 6 6 12 12 11 5 9 9 5 0 3 7 8 
  table> 4 15 1 12 14 8 8 2 13 4 6 9 2 1 11 7 
  table> 15 5 12 11 9 3 7 14 3 10 10 0 5 6 0 13 
end-table

eetable subst2
  table> 15 3 1 13 8 4 14 7 6 15 11 2 3 8 4 14 
  table> 9 12 7 0 2 1 13 10 12 6 0 9 5 11 10 5 
  table> 0 13 14 8 7 10 11 1 10 3 4 15 13 4 1 2 
  table> 5 11 8 7 12 7 6 12 9 0 3 5 2 14 15 9 
end-table

eetable subst3
  table> 10 13 0 7 9 0 14 9 6 3 3 4 15 6 5 10 
  table> 1 2 13 8 12 5 7 14 11 12 4 11 2 15 8 1 
  table> 13 1 6 10 4 13 9 0 8 6 15 9 3 8 0 7 
  table> 11 4 1 15 2 14 12 3 5 11 10 5 14 2 7 12 
end-table

eetable subst4
  table> 7 13 13 8 14 11 3 5 0 6 6 15 9 0 10 3 
  table> 1 4 2 7 8 2 5 12 11 1 12 10 4 14 15 9 
  table> 10 3 6 15 9 0 0 6 12 10 11 1 7 13 13 8 
  table> 15 9 1 4 3 5 14 11 5 12 2 7 8 2 4 14 
end-table

eetable subst5
  table> 2 14 12 11 4 2 1 12 7 4 10 7 11 13 6 1 
  table> 8 5 5 0 3 15 15 10 13 3 0 9 14 8 9 6 
  table> 4 11 2 8 1 12 11 7 10 1 13 14 7 2 8 13 
  table> 15 6 9 15 12 0 5 9 6 10 3 4 0 5 14 3 
end-table

eetable subst6
  table> 12 10 1 15 10 4 15 2 9 7 2 12 6 9 8 5 
  table> 0 6 13 1 3 13 4 14 14 0 7 11 5 3 11 8 
  table> 9 4 14 3 15 2 5 12 2 9 8 5 12 15 3 10 
  table> 7 11 0 14 4 1 10 7 1 6 13 0 11 8 6 13 
end-table

eetable subst7
  table> 4 13 11 0 2 11 14 7 15 4 0 9 8 1 13 10 
  table> 3 14 12 3 9 5 7 12 5 2 10 15 6 8 1 6 
  table> 1 6 4 11 11 13 13 8 12 1 3 4 7 10 14 7 
  table> 10 9 15 5 6 0 8 15 0 14 5 2 9 3 2 12 
end-table

eetable subst8
  table> 13 1 2 15 8 13 4 8 6 10 15 3 11 7 1 4 
  table> 10 12 9 5 3 6 14 11 5 0 0 14 12 9 7 2 
  table> 7 2 11 1 4 14 1 7 9 4 12 10 14 8 2 13 
  table> 0 15 6 12 10 9 13 0 15 3 3 5 5 6 8 11 
end-table

hex

\ Extract 6 bits values from 8 bits values
: norm ( b -- b' ) 3f and ;
: 6l ( -- b ) src@ 2 rshift ;
: 2r4l ( -- b ) src@++ 4 lshift src@ 4 rshift or norm ;
: 4r2l ( -- b ) src@++ 2 lshift src@ 6 rshift or norm ;
: 6r ( -- b ) src@++ norm ;

: l! ( b1 -- b1' ) 4 lshift ;
: r! ( b1' b2 -- ) or dst!++ ;

\ Perform substitution from the 48 bits value in src to a 32 bits value in dst
: subst ( -- )
    6l subst1 l! 2r4l subst2 r! 4r2l subst3 l! 6r subst4 r!
    6l subst5 l! 2r4l subst6 r! 4r2l subst7 l! 6r subst8 r!
;

\ Expansion of a 32 bits value into a 48 bits value

: dupl1 ( b -- ) dup 8 and if 2 or then dup 4 and if 1 or then dst!++ ;
: dupl2 ( b -- ) dup 20 and if 8 or then dup 4 and if 10 or then dst!++ ;
: dupl3 ( b -- ) dup 20 and if 80 or then dup 10 and if 40 or then dst!++ ;

: expand1 ( -- ) src@ 1 rshift src @ 3 + @ 1 and if 80 or then dupl1 ;
: expand2 ( -- ) src@++ 5 lshift src@ 5 rshift or dupl2 ;
: expand3 ( -- ) src@++ 1 lshift src@ 80 and if 1 or then 3f and dupl3 ;
: expand4 ( -- ) src@ 2 lshift dupl1 ;
\ expand5 is similar to expand2
: expand6 ( -- ) src@ 1 lshift src @ 3 - @ 80 and if 1 or then dupl3 ;

: expand ( -- ) expand1 expand2 expand3 expand4 expand2 expand6 ;

main : test ( -- ) ip nop ;
