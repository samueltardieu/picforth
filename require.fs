\ require.fs

\ Authors: Anton Ertl, Bernd Paysan, Neal Crook, Jens Wilke
\ Copyright (C) 1995,1996,1997,1998,2000,2003,2006,2007,2010,2012,2013,2016,2017,2019,2022,2023 Free Software Foundation, Inc.

\ This file is part of Gforth.

\ Gforth is free software; you can redistribute it and/or
\ modify it under the terms of the GNU General Public License
\ as published by the Free Software Foundation, either version 3
\ of the License, or (at your option) any later version.

\ This program is distributed in the hope that it will be useful,
\ but WITHOUT ANY WARRANTY; without even the implied warranty of
\ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
\ GNU General Public License for more details.

\ You should have received a copy of the GNU General Public License
\ along with this program. If not, see http://www.gnu.org/licenses/.

\ Now: Kernel Module, Reloadable

\ PicForth: included-files, sourcefilename, sourceline#, included? and
\ add-included-file are reused from the Gforth kernel: the kernel's
\ path expansion (expandtopic) resolves relative names through them.

: included1 ( i*x file-id c-addr u -- j*x ) \ gforth-internal
\G Include the file file-id with the name given by @var{c-addr u}.
    add-included-file  included-files $@ + cell-
    $@ ['] read-loop execute-parsing-named-file ;

Defer >included ( c-addr1 u1 -- fd c-addr2 u2 wior )
' open-fpath-file is >included
Defer >include ( c-addr1 u1 -- c-addr2 u2 )
' noop is >include

variable include-auto-extension-filename
: >include-auto-extension ( c-addr1 u1 -- fd c-addr2 u2 wior )
    2>r 2r@ open-fpath-file dup if \ if not found extend with .4th suffix
        drop \ ignore error
        2r> include-auto-extension-filename $!
        s" .4th" include-auto-extension-filename $+!
        include-auto-extension-filename $@
        open-fpath-file
    else
        2rdrop \ drop filename
    then ;
' >include-auto-extension is >included

: included ( i*x c-addr u -- j*x ) \ file
    \G @code{include-file} the file whose name is given by the string
    \G @var{c-addr u}.
    >included throw included1 ;

: required ( i*x addr u -- i*x ) \ file-ext
    \G @code{include-file} the file with the name given by @var{addr
    \G u}, if it is not @code{included} (or @code{required})
    \G already. Currently this works by comparing the name of the file
    \G (with path) against the names of earlier included files.
    \ however, it may be better to fstat the file,
    \ and compare the device and inode. The advantages would be: no
    \ problems with several paths to the same file (e.g., due to
    \ links) and we would catch files included with include-file and
    \ write a require-file.
    >included throw 2dup included?
    if
	2drop close-file throw
    else
	included1
    then ;

\ INCLUDE                                               9may93jaw

: include  ( ... "file" -- ... ) \ file-ext
    \G @code{include-file} the file @var{file}.
    ?parse-name >include included ;

: require  ( ... "file" -- ... ) \ file-ext
    \G @code{include-file} @var{file} only if it is not included already.
    ?parse-name >include required ;

\ : \I
\   here 
\   0 word count
\   string,
\   needsrcs^ @ ! ;

\ : .included ( -- ) \ gforth
\     \G list the names of the files that have been @code{included}
\   cr
\   needs^ @
\   BEGIN		dup 
\   WHILE		dup cell+ count type cr
\ 		5 spaces
\ 		dup cell+ count + aligned
\ 		@ dup IF count type ELSE drop THEN cr
\ 		@
\   REPEAT
\   drop ;

: .included ( -- ) \ gforth
    \G List the names of the files that have been @code{included}.
    included-files $@ bounds ?DO
	cr I $@ type
    cell +LOOP ;
