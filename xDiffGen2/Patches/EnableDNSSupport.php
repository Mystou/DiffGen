<?php

    function EnableDNSSupport($exe){
        if ($exe === true) {
            return new xPatch(90, 'EnableDNSSupport', 'UI', 0, 
"Enable DNS support for clientinfo.xml");
        }
        
        $code =  "\xE8\xAB\xAB\xAB\xFF\x8B\xC8\xE8\xAB\xAB\xAB\xFF\x50\xB9\xAB\xAB\xAB\x00\xE8\xAB\xAB\xAB\xFF\xA1";

        $offset = $exe->code($code, "\xAB");
		if ($offset === false) {
			echo "Failed in part 1";
			return false;
		}
		
        $offsetRVA = $exe->Raw2Rva($offset) + $exe->read($offset + 1, 4, "I") + 5;
        
        $code =  
			// Call Unknown Function - Pos = 1
			"\xE8\x00\x00\x00\x00"						// CALL UnknownCall
			."\x60"										// PUSHAD
			// Pointer of old address - Pos = 8
			."\x8B\x35\x00\x00\x00\x00"					// MOV ESI,DWORD PTR DS:[7F8320]            ; ASCII "127.0.0.1"
			."\x56"										// PUSH ESI
			// Call to gethostbyname - Pos = 15
			."\xFF\x15\x00\x00\x00\x00"					// CALL DWORD PTR DS:[<&WS2_32.#52>]
			."\x8B\x48\x0C"								// MOV ECX,DWORD PTR DS:[EAX+0C]
			."\x8B\x11"									// MOV EDX,DWORD PTR DS:[ECX]
			."\x89\xD0"									// MOV EAX,EDX
			."\x0F\xB6\x48\x03"							// MOVZX ECX,BYTE PTR DS:[EAX+3]
			."\x51"										// PUSH ECX
			."\x0F\xB6\x48\x02"							// MOVZX ECX,BYTE PTR DS:[EAX+2]
			."\x51"										// PUSH ECX
			."\x0F\xB6\x48\x01"							// MOVZX ECX,BYTE PTR DS:[EAX+1]
			."\x51"										// PUSH ECX
			."\x0F\xB6\x08"								// MOVZX ECX,BYTE PTR DS:[EAX]
			."\x51"										// PUSH ECX
			// IP scheme offset - Pos = 46
			."\x68\x00\x00\x00\x00"						// PUSH OFFSET 007B001C                     ; ASCII "%d.%d.%d.%d"
			// Pointer to new address Pos = 51
			."\x68\x00\x00\x00\x00"						// PUSH OFFSET 008A077C                     ; ASCII "127.0.0.1"
			// Call to sprintf - Pos = 57
			."\xFF\x15\x00\x00\x00\x00"					// CALL DWORD PTR DS:[<&MSVCR90.sprintf>]
			."\x83\xC4\x18"								// ADD ESP,18
			// Replace old ptr with new ptr
			// Old Ptr - Pos = 66
			// New Ptr - Pos = 70
			."\xC7\x05\x00\x00\x00\x00\x00\x00\x00\x00"	// MOV DWORD PTR DS:[7F8320],OFFSET 008A07C ; ASCII "127.0.0.1"
			."\x61"										// POPAD
			."\xC3";									// RETN
			                    
        // Calculate free space that the code will need.
        $size = strlen($code);
        
        // Find free space to inject our data.ini load function.
        // Note that for the time beeing those will be probably
        // return some space in .rsrc, but that's still okay
        // until our new diff patcher is finished for our own section.
        $free = $exe->zeroed(247 + 4 + 4 + $size + 4 + 16, false); // Free space of enable multiple grf + space for dns support
        if ($free === false) {
            echo "Failed in part 2";
            return false;
        }
		$free += 247 + 4 + 4;
        
        // Create a call to the free space that was found before.     
        $exe->replace($offset, array(0 =>  "\xE8".pack("I", $exe->Raw2Rva($free) - $exe->Raw2Rva($offset) - 5 + 2 + 16) ));

        /************************************************************************/
		/* Find old ptr.
		/************************************************************************/
        
        $code =  "\xA3\xAB\xAB\xAB\x00\xEB\x0F\x83\xC0\x04\xA3\xAB\xAB\xAB\x00\xEB\x05";

        $offset = $exe->code($code, "\xAB");
		if ($offset === false) {
			echo "Failed in part 3";
			return false;
		}
		$uOldptr = $exe->read($offset + 1, 4, "I");
        
        /************************************************************************/
		/* Find gethostbyname().
		/************************************************************************/
		
        $code = "\xFF\x15\xAB\xAB\xAB\x00\x85\xC0\x75\x29\x8B\xAB\xAB\xAB\xAB\x00";

        $offset = $exe->code($code, "\xAB");
		if ($offset === false) {
			$code =  "\xE8\xAB\xAB\xAB\x00\x85\xC0\x75\x35\x8B\xAB\xAB\xAB\xAB\x00";
			$offset = $exe->code($code, "\xAB");
			if ($offset === false) {
				echo "Failed in part 4";
				return false;
			}
			else {
				$offset = $exe->Raw2Rva($offset) + $exe->read($offset + 1, 4, "I") + 5;
				$uGethostbyname = $exe->read($offset, 4, "I") +2;
			}
		}
		else {
			$uGethostbyname = $exe->read($offset + 2, 4, "I");
		}
        
		$uSprintf = $exe->func("sprintf");
        if ($uSprintf === false) {
            echo "Failed in part 5";
            return false;
        }
		
		$uIPScheme = $exe->str("'%d.%d.%d.%d'","raw");
        if ($uIPScheme === false) {
            echo "Failed in part 6";
            return false;
        }
        
        return true;
    }
?>