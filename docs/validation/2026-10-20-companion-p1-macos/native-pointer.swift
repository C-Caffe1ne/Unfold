import AppKit
import CoreGraphics
import Foundation
let args=CommandLine.arguments
let pid:pid_t=87918
func windows()->[[String:Any]] {
 let list=CGWindowListCopyWindowInfo([.optionOnScreenOnly,.excludeDesktopElements],kCGNullWindowID) as? [[String:Any]] ?? []
 return list.filter{ ($0[kCGWindowOwnerPID as String] as? Int) == Int(pid) }
}
func belongs(_ p:CGPoint)->Bool { windows().contains { w in
 guard let b=w[kCGWindowBounds as String] as? NSDictionary, let r=CGRect(dictionaryRepresentation:b) else{return false};return r.contains(p)
}}
func move(_ p:CGPoint) {
 CGWarpMouseCursorPosition(p)
 CGEvent(mouseEventSource:nil,mouseType:.mouseMoved,mouseCursorPosition:p,mouseButton:.left)?.post(tap:.cghidEventTap)
 Thread.sleep(forTimeInterval:0.35)
}
func point(_ i:Int)->CGPoint {CGPoint(x:Double(args[i])!,y:Double(args[i+1])!)}
if args.count>1 && args[1]=="snapshot" {
 let data=try JSONSerialization.data(withJSONObject:windows(),options:[.prettyPrinted,.sortedKeys]);print(String(data:data,encoding:.utf8)!)
 let p=CGEvent(source:nil)!.location; print("CURSOR \(p.x),\(p.y)")
} else if args.count==4 && args[1]=="move" {
 let p=point(2); guard belongs(p) else {fatalError("Pointer target outside owned test windows")};move(p)
} else if args.count==4 && args[1]=="click" {
 let p=point(2);guard belongs(p) else{fatalError("Click outside owned test windows")};move(p)
 CGEvent(mouseEventSource:nil,mouseType:.leftMouseDown,mouseCursorPosition:p,mouseButton:.left)?.post(tap:.cghidEventTap)
 Thread.sleep(forTimeInterval:0.08)
 CGEvent(mouseEventSource:nil,mouseType:.leftMouseUp,mouseCursorPosition:p,mouseButton:.left)?.post(tap:.cghidEventTap)
 Thread.sleep(forTimeInterval:0.3)
} else if args.count==6 && args[1]=="drag" {
 let from=point(2),to=point(4);guard belongs(from) else{fatalError("Drag start outside owned test windows")}
 move(from)
 CGEvent(mouseEventSource:nil,mouseType:.leftMouseDown,mouseCursorPosition:from,mouseButton:.left)?.post(tap:.cghidEventTap)
 Thread.sleep(forTimeInterval:0.2)
 for i in 1...35 {let t=Double(i)/35;let p=CGPoint(x:from.x+(to.x-from.x)*t,y:from.y+(to.y-from.y)*t);CGEvent(mouseEventSource:nil,mouseType:.leftMouseDragged,mouseCursorPosition:p,mouseButton:.left)?.post(tap:.cghidEventTap);Thread.sleep(forTimeInterval:0.018)}
 CGWarpMouseCursorPosition(to)
 CGEvent(mouseEventSource:nil,mouseType:.leftMouseUp,mouseCursorPosition:to,mouseButton:.left)?.post(tap:.cghidEventTap)
 Thread.sleep(forTimeInterval:0.5)
} else {print("Usage: snapshot | move x y | drag x1 y1 x2 y2");exit(2)}
